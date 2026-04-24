import { Route } from '@angular/router';

export default [
  {
    path: 'search',
    loadComponent: () =>
      import('./pages/slot-search/slot-search.component').then(
        (m) => m.SlotSearchComponent,
      ),
    title: 'Find an Appointment — PropelIQ',
  },
  {
    path: 'intake',
    loadComponent: () =>
      import('./pages/intake-form/intake-form.component').then(
        (m) => m.IntakeFormComponent,
      ),
    title: 'Patient Intake Form — PropelIQ',
  },
  {
    // SCR-006: Booking Confirmation page — loaded after POST /api/v1/bookings (AC-1).
    // Route with appointmentId for direct link / reload from router state.
    path: 'booking/confirmation/:appointmentId',
    loadComponent: () =>
      import('./pages/booking-confirmation/booking-confirmation.component').then(
        (m) => m.BookingConfirmationComponent,
      ),
    title: 'Booking Confirmed — PropelIQ',
  },
  {
    // SCR-006: Booking Confirmation page — navigated with booking in router state
    // (no appointmentId in URL) right after a successful POST /api/v1/bookings.
    path: 'booking/confirmation',
    loadComponent: () =>
      import('./pages/booking-confirmation/booking-confirmation.component').then(
        (m) => m.BookingConfirmationComponent,
      ),
    title: 'Booking Confirmed — PropelIQ',
  },
] satisfies Route[];
