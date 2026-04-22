using Microsoft.AspNetCore.Authorization;

namespace PropelIQ.Api.Authorization.Requirements;

/// <summary>
/// Marker requirement for patient-scoped resource access (AC-2).
/// Evaluated by <see cref="Handlers.PatientResourceAuthorizationHandler"/>.
/// Staff and Admin bypass this requirement; Patients must match the route patientId.
/// </summary>
public sealed class PatientResourceRequirement : IAuthorizationRequirement { }
