import { Component, DestroyRef, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Employee, LeaveRequest, LeaveStatus, LeaveType } from '../models/leave-request.model';
import { LeaveRequestsService } from '../services/leave-requests.service';
import { finalize } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-leave-requests',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './leave-requests.component.html',
  styleUrls: ['./leave-requests.component.css']
})
export class LeaveRequestsComponent implements OnInit {
  private readonly service = inject(LeaveRequestsService);
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  // exposed to the template
  readonly LeaveStatus = LeaveStatus;
  readonly typeOptions = [
    { value: LeaveType.Vacation, label: 'Vacation' },
    { value: LeaveType.Sick, label: 'Sick' },
    { value: LeaveType.Unpaid, label: 'Unpaid' }
  ];
  readonly typeLabels: Record<LeaveType, string> = {
    [LeaveType.Vacation]: 'Vacation',
    [LeaveType.Sick]: 'Sick',
    [LeaveType.Unpaid]: 'Unpaid'
  };
  readonly statusLabels: Record<LeaveStatus, string> = {
    [LeaveStatus.Pending]: 'Pending',
    [LeaveStatus.Approved]: 'Approved',
    [LeaveStatus.Rejected]: 'Rejected'
  };

  requests: LeaveRequest[] = [];
  employees: Employee[] = [];
  loading = false;
  loadError: string | null = null;
  employeesError: string | null = null;

  submitting = false;
  formError: string | null = null;
  readonly approvingIds = new Set<number>();
  notice: Notice | null = null;

  readonly form = this.fb.group(
    {
      employeeId: this.fb.control<number | null>(null, Validators.required),
      type: this.fb.control<LeaveType | null>(null, Validators.required),
      startDate: this.fb.control('', Validators.required),
      endDate: this.fb.control('', Validators.required)
    },
    { validators: dateRangeValidator }
  );

  private apiUrl = 'http://localhost:53578/api/leave-requests';

  constructor(private http: HttpClient) { }

  ngOnInit(): void {
    this.load();
    this.loadEmployees();
  }

  load(showSpinner = true): void {
    if (showSpinner) this.loading = true;
    this.loadError = null;
    this.service.getAll()
      .pipe(finalize(() => (this.loading = false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (data) => (this.requests = data),
        error: (err: unknown) =>
          (this.loadError = this.toMessage(err, 'Could not load leave requests.'))
      });
  }

  private loadEmployees(): void {
    this.service.getEmployees()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (data) => (this.employees = data),
        error: (err: unknown) =>
          (this.employeesError = this.toMessage(err, 'Could not load employees.'))
      });
  }

  // ---- form ----
  isInvalid(name: 'employeeId' | 'type' | 'startDate' | 'endDate'): boolean {
    const c = this.form.controls[name];
    return c.invalid && c.touched;
  }

  get showRangeError(): boolean {
    return this.form.hasError('dateRange') &&
      (this.form.controls.startDate.touched || this.form.controls.endDate.touched);
  }

  get daysPreview(): number | null {
    const { startDate, endDate } = this.form.getRawValue();
    if (!startDate || !endDate || this.form.hasError('dateRange')) return null;
    return daysInclusive(startDate, endDate);
  }

  submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.submitting) return;

    const { employeeId, type, startDate, endDate } = this.form.getRawValue();
    if (employeeId === null || type === null) return;

    this.submitting = true;
    this.formError = null;
    this.service.create({ employeeId, type, startDate, endDate })
      .pipe(finalize(() => (this.submitting = false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.form.reset();
          this.notice = { kind: 'success', text: 'Leave request submitted.' };
          this.load(false); // refresh is needed here: ordering + employee name
        },
        error: (err: unknown) =>
          (this.formError = this.toMessage(err, 'Could not submit the request. Please try again.'))
      });
  }

  // ---- approve ----
  approve(id: number): void {
    if (this.approvingIds.has(id)) return;
    this.approvingIds.add(id);
    this.notice = null;

    this.service.approve(id)
      .pipe(finalize(() => this.approvingIds.delete(id)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          // update only the affected row, no blind reload
          this.requests = this.requests.map((r) =>
            r.id === id ? { ...r, status: LeaveStatus.Approved } : r);
          this.notice = { kind: 'success', text: `Request #${id} approved.` };
        },
        error: (err: unknown) => {
          this.notice = {
            kind: 'error',
            text: this.toMessage(err, `Could not approve request #${id}.`)
          };
          // our view is stale (already handled / missing) -> resync quietly
          if (err instanceof HttpErrorResponse && (err.status === 404 || err.status === 409)) {
            this.load(false);
          }
        }
      });
  }

  dismissNotice(): void { this.notice = null; }
  trackById(_: number, r: LeaveRequest): number { return r.id; }

  private toMessage(err: unknown, fallback: string): string {
    if (!(err instanceof HttpErrorResponse)) return fallback;
    if (err.status === 0) return 'Cannot reach the server. Check your connection and try again.';
    if (err.status >= 400 && err.status < 500) { // only trust messages for client errors
      const body = err.error as ProblemDetails | string | null;
      if (typeof body === 'string' && body.trim()) return body;
      if (body && typeof body === 'object') {
        const msgs = Object.values(body.errors ?? {}).flat();
        if (msgs.length) return msgs.join(' ');
        if (body.title) return body.title;
      }
    }
    return fallback;
  }

  typeLabel(type: number): string {
    if (type == 0) return 'Vacation';
    if (type == 1) return 'Sick';
    return 'Unpaid';
  }

  statusLabel(status: number): string {
    if (status == 0) return 'Pending';
    if (status == 1) return 'Approved';
    return 'Rejected';
  }
}

interface ProblemDetails { title?: string; errors?: Record<string, string[]>; }
interface Notice { kind: 'success' | 'error'; text: string; }

// ISO yyyy-MM-dd strings compare correctly as plain strings.
function dateRangeValidator(group: AbstractControl): ValidationErrors | null {
  const start = group.get('startDate')?.value as string;
  const end = group.get('endDate')?.value as string;
  return start && end && end < start ? { dateRange: true } : null;
}

function daysInclusive(start: string, end: string): number {
  const toUtc = (iso: string) => {
    const [y, m, d] = iso.split('-').map(Number);
    return Date.UTC(y, m - 1, d);
  };
  return (toUtc(end) - toUtc(start)) / 86_400_000 + 1;
}