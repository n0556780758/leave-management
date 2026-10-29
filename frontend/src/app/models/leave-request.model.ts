// Intentionally thin. Part of the task is to introduce proper typing
// across the frontend instead of the `any` usage in the component.

export interface Employee {
  id: number;
  name: string;
  annualQuota: number;
}

export interface LeaveRequest {
  id: number;
  employeeId: number;
  // Populated by the API on reads; absent on the response to a create.
  employee?: Employee;
  type: number;
  startDate: string;
  endDate: string;
  status: number;
  days: number;
}

export interface CreateLeaveRequestDto {
  employeeId: number;
  type: LeaveType;
  startDate: string; // 'yyyy-MM-dd' from <input type="date">
  endDate: string;
}

export enum LeaveType { Vacation = 0, Sick = 1, Unpaid = 2 }
export enum LeaveStatus { Pending = 0, Approved = 1, Rejected = 2 }
