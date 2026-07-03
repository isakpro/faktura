// Speglar backend-kontraktet (contracts/rest-api.md).
export interface UserDto {
  id: string;
  email: string;
  role: "Owner" | "Admin" | "Member";
}

export interface OrganizationDto {
  id: string;
  name: string;
  plan: "Free" | "Pro";
  subscriptionStatus: string;
  seatLimit: number;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: UserDto;
  organization: OrganizationDto;
}

export interface MeResponse {
  user: UserDto;
  organization: OrganizationDto;
}

export interface MemberDto {
  id: string;
  email: string;
  role: string;
}

export interface InvitationDto {
  id: string;
  email: string;
  role: string;
  status: string;
}

export interface BillingDto {
  plan: string;
  subscriptionStatus: string;
  seatLimit: number;
}
