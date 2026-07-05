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

export interface CustomerDto {
  id: string;
  name: string;
  email?: string | null;
  orgNumber?: string | null;
  vatNumber?: string | null;
  paymentTermsDays: number;
  status: string;
}

export interface InvoiceLineInput {
  description: string;
  quantity: number;
  unitPriceExclVat: number;
  vatRate: number;
  unit?: string | null;
}

export interface ArticleDto {
  id: string;
  name: string;
  sku?: string | null;
  unit?: string | null;
  unitPriceExclVat: number;
  vatRate: number;
  status: string;
}

export interface VatByRateDto {
  rate: number;
  vat: number;
}

export interface InvoiceTotalsDto {
  net: number;
  vatByRate: VatByRateDto[];
  gross: number;
}

export interface InvoiceLineDto extends InvoiceLineInput {
  net: number;
  vat: number;
}

export interface InvoiceDto {
  id: string;
  type: string;
  status: string;
  number?: number | null;
  customerId: string;
  invoiceDate?: string | null;
  dueDate?: string | null;
  paidDate?: string | null;
  originalInvoiceId?: string | null;
  lines: InvoiceLineDto[];
  totals: InvoiceTotalsDto;
}

export interface ReminderSettingsDto {
  autoEnabled: boolean;
  daysAfterDue: number;
}

export interface InvoiceListItemDto {
  id: string;
  number?: number | null;
  status: string;
  customerId: string;
  gross: number;
  dueDate?: string | null;
}
