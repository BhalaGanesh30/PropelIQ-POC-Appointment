# Unit Test Plan - TASK_001

## Requirement Reference
- **User Story**: us_013
- **Story Location**: .propel/context/tasks/EP-001/us_013/us_013.md
- **Layer**: BE
- **Related Test Plans**: N/A (single unified plan for this workflow run)
- **Acceptance Criteria Covered**:
  - AC-1: Register with valid email and password creates pending account and sends verification message within SLA intent
  - AC-2: Email confirmation activates account and writes audit event
  - AC-3: Phone OTP flow sends six-digit OTP and activates verification path after successful check
  - AC-4: Duplicate email or phone submission returns account-exists response without verification-state leakage
- **Requirement Tags**:
  - FR-UM-001
  - NFR-007
  - NFR-012
  - UXR-101

## Test Plan Overview
This plan covers unit tests for backend registration and verification behavior in API and validation layers, focusing on account creation, duplicate prevention, email confirmation, OTP issuance and verification, and security-sensitive error handling. The tests are isolated using mocks for Identity, cache, notification delivery, and persistence boundaries, with no dependency on data from other user stories.

## Dependent Tasks
- TASK_001: Backend registration API
- US_002 foundational auth infrastructure (mocked in unit scope)
- US_009 foundational user model (mocked in unit scope)

## Components Under Test

| Component | Type | File Path | Responsibilities |
|-----------|------|-----------|------------------|
| AuthController | controller | server/src/PropelIQ.Api/Controllers/AuthController.cs | Registration, confirmation, OTP, login and token flow endpoints |
| RegisterRequestValidator | validator | server/src/Modules/Administration/PropelIQ.Modules.Administration.Application/Auth/Validators/RegisterRequestValidator.cs | Input validation for register payload |
| RegisterRequest | record | server/src/Modules/Administration/PropelIQ.Modules.Administration.Application/Auth/RegisterRequest.cs | Registration request contract |
| SendOtpRequest | record | server/src/Modules/Administration/PropelIQ.Modules.Administration.Application/Auth/OtpRequests.cs | OTP send request contract |
| VerifyOtpRequest | record | server/src/Modules/Administration/PropelIQ.Modules.Administration.Application/Auth/OtpRequests.cs | OTP verification request contract |

## Test Cases

| Test-ID | Type | Description | Given | When | Then | Assertions |
|---------|------|-------------|-------|------|------|------------|
| TC-001 | positive | Register succeeds for new email | No existing user for email, valid payload | Register endpoint is called | API returns Accepted and creates user | Result is 202; CreateAsync called once; notification send called once |
| TC-002 | positive | Register assigns Patient role | User creation succeeds | Register endpoint continues post-create | User gets Patient role membership | AddToRoleAsync called with Patient; role guard executed |
| TC-003 | positive | Confirm email succeeds | Existing user and valid token | ConfirmEmail endpoint is called | Account is confirmed and success response returned | Result is 200; ConfirmEmailAsync called once |
| TC-004 | positive | Send OTP stores code and responds accepted | Existing user found by email | SendOtp endpoint is called | OTP saved with TTL and accepted response returned | Cache set called with key propeliq:otp:<userId>; TTL equals 10 minutes; result is 202 |
| TC-005 | positive | Verify OTP succeeds and consumes token | User exists and cached OTP matches | VerifyOtp endpoint is called with valid OTP | OTP is removed and phone is marked confirmed | Result is 200; cache remove called once; user.PhoneNumberConfirmed becomes true; UpdateAsync called once |
| TC-006 | negative | Duplicate registration is blocked | Existing user found by email | Register endpoint is called | Conflict problem is returned | Result is 409; CreateAsync not called; no role assignment call |
| TC-007 | negative | Registration create failure returns validation problem | CreateAsync returns Identity errors | Register endpoint is called | ValidationProblem response is returned | Result is validation response; model state has Identity errors |
| TC-008 | negative | Role assignment failure triggers rollback | CreateAsync succeeds, AddToRoleAsync fails | Register endpoint is called | User is deleted and validation problem is returned | DeleteAsync called once; response is validation response |
| TC-009 | negative | Confirm email with invalid token fails | Existing user and invalid token | ConfirmEmail endpoint is called | Bad request problem is returned | Result is 400; includes failure details |
| EC-001 | edge_case | Email exactly at max length is accepted by validator | Email length is 256 and format valid | RegisterRequestValidator validates payload | Validation passes for email length rule | No email-length validation failure |
| EC-002 | edge_case | E.164 phone accepted | Phone number in E.164 format | RegisterRequestValidator validates payload | Validation passes for phone rule | No phone-format validation failure |
| EC-003 | edge_case | OTP mismatch fails with generic message | Stored OTP and submitted OTP differ by one digit | VerifyOtp endpoint is called | Generic invalid-or-expired message returned | Result is 400; no indication whether OTP was close |
| ES-001 | error | OTP cache missing behaves as expired token | User exists but cached OTP is absent | VerifyOtp endpoint is called | API rejects request safely | Result is 400; UpdateAsync not called |
| ES-002 | error | OTP email send exception does not crash request | User exists but email sender throws | SendOtp endpoint is called | Request still returns accepted with safe behavior | Result is 202; exception handled internally |
| ES-003 | error | OTP SMS send exception does not crash request | User has phone but SMS sender throws | SendOtp endpoint is called | Request still returns accepted | Result is 202; exception handled internally |

## AI Component Test Cases
AI impact is not in scope for us_013. No AIR-XXX requirements are mapped, so this section is intentionally not applicable.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/tests/PropelIQ.Api.Tests/Auth/AuthControllerRegistrationTests.cs | Register, confirm-email, send-otp, verify-otp controller unit tests |
| CREATE | server/tests/PropelIQ.Api.Tests/Auth/RegisterRequestValidatorTests.cs | Validator-focused unit tests for payload quality and boundary conditions |
| CREATE | .propel/context/tasks/EP-001/us_013/unittest/test_data/fixtures.json | Synthetic registration, OTP, and duplicate-account fixtures |
| CREATE | .propel/context/tasks/EP-001/us_013/unittest/test_data/edge_cases.json | Boundary and malformed input samples |
| CREATE | .propel/context/tasks/EP-001/us_013/unittest/mocks/mock_identity_dependencies.cs | UserManager/SignInManager/RoleManager and cache mock helper patterns |

## Mocking Strategy

| Dependency | Mock Type | Mock Behavior | Return Value |
|------------|-----------|---------------|--------------|
| UserManager<ApplicationUser> | mock | FindByEmailAsync returns hit/miss by scenario | ApplicationUser or null |
| RoleManager<IdentityRole<Guid>> | mock | RoleExistsAsync and CreateAsync controlled per test | true/false and IdentityResult |
| SignInManager<ApplicationUser> | mock | For login related cases only, returns controlled SignInResult | Success/Failed/LockedOut |
| IDistributedCache | mock | Stores and retrieves OTP value per cache key | OTP string or null |
| INotificationSender | mock | Sends email/SMS without network calls | completed task or thrown exception |
| AppDbContext | stub/mock | Audit write path isolated from real database | No-op for unit tests |
| IConfiguration | stub | Returns API/client base URLs deterministically | local test URLs |
| ILogger<AuthController> | spy | Captures warning/error log calls for assertions | call count and message checks |

## AI Mocking Strategy
Not applicable because there are no AIR-XXX requirements in us_013 and no LLM dependency in scope.

## Test Data

| Scenario | Input Data | Expected Output |
|----------|------------|-----------------|
| Valid registration | Email, strong password, names, optional phone | Accepted response and create + role calls |
| Duplicate account | Existing email in user store | Conflict problem response |
| Role assignment failure | Identity role add returns failed result | Validation problem and user rollback |
| Valid OTP verify | Stored OTP equals submitted OTP | Success response and consumed OTP |
| Expired OTP | No OTP in cache key | Invalid or expired OTP response |
| Invalid phone format | Non-E.164 phone input | Validator failure message |

## Test Commands
- **Run Tests**: dotnet test server/tests/PropelIQ.Api.Tests/PropelIQ.Api.Tests.csproj
- **Run with Coverage**: dotnet test server/tests/PropelIQ.Api.Tests/PropelIQ.Api.Tests.csproj --collect:"XPlat Code Coverage"
- **Run Single Test Class**: dotnet test server/tests/PropelIQ.Api.Tests/PropelIQ.Api.Tests.csproj --filter "FullyQualifiedName~AuthControllerRegistrationTests"

## Coverage Target
- **Line Coverage**: 85%
- **Branch Coverage**: 80%
- **Critical Paths**:
  - Register happy path and duplicate conflict path at 100% branch coverage
  - Role assignment rollback path at 100% branch coverage
  - OTP verification success and invalid/expired path at 100% branch coverage

## Documentation References
- **Framework Docs**: xUnit.net (Context7: /websites/xunit_net)
- **Project Test Patterns**: server/tests/PropelIQ.Api.Tests/Authorization/AuthorizationCoverageTests.cs
- **Mocking Guide**: Moq usage conventions for .NET xUnit test projects

## Implementation Checklist
- [ ] Create test file structure per Expected Changes
- [ ] Set up synthetic fixtures in unittest/test_data
- [ ] Configure dependency mocks per Mocking Strategy
- [ ] Implement positive test cases (TC-001 to TC-005)
- [ ] Implement negative test cases (TC-006 to TC-009)
- [ ] Implement edge case tests (EC-001 to EC-003)
- [ ] Implement error scenario tests (ES-001 to ES-003)
- [ ] Run test suite and validate coverage targets

## AI Test Implementation Checklist
- [ ] Not applicable for this story (no AIR-XXX scope)
