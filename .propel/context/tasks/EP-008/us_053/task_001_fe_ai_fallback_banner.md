---
task_id: task_001
user_story: us_053
epic: EP-008
layer: Frontend
status: not-started
effort_hours: 3
---

# Task - task_001

## Requirement Reference

- **User Story**: [us_053] AI Gateway Integration with Circuit Breaker Fallback
- **Story Location**: [.propel/context/tasks/EP-008/us_053/us_053.md](.propel/context/tasks/EP-008/us_053/us_053.md)
- **Acceptance Criteria**:
  - AC-2: Given the AI provider returns errors for 5 consecutive requests, When the circuit breaker threshold is reached, Then the circuit opens and all subsequent AI requests return a fallback response — the FE must display the fallback banner on all AI-dependent screens.
  - AC-3: Given the circuit breaker is open, When a half-open probe succeeds, Then the circuit closes and AI-assisted requests resume — the FE fallback banner must dismiss when AI is restored.
- **Edge Cases**:
  - Edge Case 1: Rapid circuit cycling — no additional FE change; banner stays visible while circuit is open per API response flag.
  - Edge Case 2: AI fallback active — a global notification banner "AI assistance temporarily unavailable. Manual coding mode is active." is displayed on all AI-dependent screens (SCR-017, SCR-014, SCR-015) while `aiFallbackActive: true` is returned in API responses.

---

## Design References (Frontend Task)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A — no dedicated screen; banner overlaid on existing AI screens |
| **Wireframe Status** | N/A |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | Global banner on SCR-017 (Coding Review), SCR-014 (360° Patient Profile), SCR-015 (Clinical Timeline) when AI fallback is active |
| **UXR Requirements** | N/A |
| **Design Tokens** | Warning amber mat-card banner; `role="status"`; `aria-live="polite"` |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Backend | N/A | N/A |
| ORM | N/A | N/A |
| Database | N/A | N/A |
| Cache | N/A | N/A |
| Observability | N/A | N/A |
| Frontend | Angular + Angular Material | 17.x |
| AI/ML | N/A | N/A |
| AI Gateway | N/A | N/A |
| Mobile | N/A | N/A |

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

---

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

---

## Task Overview

Implement a globally-scoped `AiFallbackBannerComponent` that is shown on all AI-dependent screens whenever an API response includes `aiFallbackActive: true` in its envelope or a dedicated `GET /api/v1/ai-gateway/status` polling response indicates the circuit is open.

The banner is amber, non-dismissable, and reads: "AI assistance temporarily unavailable. Manual coding mode is active." It uses `role="status"` and `aria-live="polite"` so screen readers are notified. When the circuit closes (`aiFallbackActive: false`), the banner disappears without a page reload.

A shared `AiGatewayStatusService` polls `GET /api/v1/ai-gateway/status` every 30 seconds when the circuit is open; polling stops when status returns `closed`. The `AiGatewayStatusFacade` exposes a `fallbackActive = signal(false)` that all AI-dependent screens consume via `@if`.

---

## Dependent Tasks

- **us_053/task_002** — Provides `GET /api/v1/ai-gateway/status` endpoint; populates `aiFallbackActive` flag in AI endpoint response envelopes.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `AiFallbackBannerComponent` | CREATE | Amber `mat-card` banner; `role="status"`; `aria-live="polite"`; shown when `facade.fallbackActive()` is true |
| `AiGatewayStatusService` | CREATE | `GET /api/v1/ai-gateway/status`; polls every 30s when fallback active; stops polling when closed |
| `AiGatewayStatusFacade` | CREATE | `fallbackActive = signal(false)`; starts polling on app init if circuit open; clears on close |
| `CodingSuggestionPanelComponent` | MODIFY | Add `@if(aiStatusFacade.fallbackActive())` `AiFallbackBannerComponent` at top of panel |
| `PatientProfileComponent` | MODIFY | Add `@if(aiStatusFacade.fallbackActive())` banner at AI-generated section heading (SCR-014) |
| `ClinicalTimelineComponent` | MODIFY | Add `@if(aiStatusFacade.fallbackActive())` banner at panel top (SCR-015) |
| `AppModule` / `AppComponent` | MODIFY | Initialize `AiGatewayStatusFacade` on app startup; inject into root |

---

## Implementation Plan

1. **Define `AiGatewayStatusDto`**: `{ circuitState: 'closed' | 'open' | 'half-open', fallbackActive: boolean, lastTripAt: string | null }`. Returned by `GET /api/v1/ai-gateway/status`.
2. **Create `AiGatewayStatusService`**: `getStatus(): Observable<AiGatewayStatusDto>` — calls `GET /api/v1/ai-gateway/status`. `startPolling(): void` — RxJS `interval(30_000)` switchMap to `getStatus()`; `takeUntil` signal that emits when `circuitState === 'closed'`. Polling starts automatically when status indicates `open` or `half-open`.
3. **Create `AiGatewayStatusFacade`**: `fallbackActive = signal(false)`. `initialize()` called on app init: calls `getStatus()` once; if `fallbackActive = true`, starts polling loop; each poll result calls `fallbackActive.set(status.fallbackActive)`. Polling stops via `takeUntil` when closed.
4. **Create `AiFallbackBannerComponent`**: `mat-card` with amber background CSS class; warning `mat-icon`; text "AI assistance temporarily unavailable. Manual coding mode is active."; `role="status"`; `aria-live="polite"`. No dismiss button — non-dismissable; disappears automatically when `fallbackActive()` returns false (Edge Case 2).
5. **Modify AI-dependent screens**: Inject `AiGatewayStatusFacade` into `CodingSuggestionPanelComponent`, `PatientProfileComponent`, `ClinicalTimelineComponent`. Add `@if(aiStatusFacade.fallbackActive()) { <app-ai-fallback-banner /> }` at the top of each component template.
6. **Register in `AppComponent`**: Call `aiStatusFacade.initialize()` in `AppComponent.ngOnInit()`. Register `AiGatewayStatusFacade` and `AiGatewayStatusService` in root-level providers.

---

## Current Project State

```
src/
├── app/
│   ├── app.component.ts                               ← MODIFY (initialize AiGatewayStatusFacade)
│   ├── shared/
│   │   ├── components/
│   │   │   └── ai-fallback-banner/
│   │   │       └── ai-fallback-banner.component.ts    ← CREATE
│   │   ├── facades/
│   │   │   └── ai-gateway-status.facade.ts            ← CREATE
│   │   └── services/
│   │       └── ai-gateway-status.service.ts           ← CREATE
│   ├── modules/
│   │   ├── clinical-intelligence/
│   │   │   ├── components/
│   │   │   │   ├── coding-suggestion-panel/
│   │   │   │   │   └── coding-suggestion-panel.component.ts ← MODIFY
│   │   │   │   ├── patient-profile/
│   │   │   │   │   └── patient-profile.component.ts   ← MODIFY
│   │   │   │   └── clinical-timeline/
│   │   │   │       └── clinical-timeline.component.ts ← MODIFY
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `shared/components/ai-fallback-banner/ai-fallback-banner.component.ts` | Amber banner; role=status; aria-live=polite; non-dismissable (Edge Case 2) |
| CREATE | `shared/facades/ai-gateway-status.facade.ts` | Signal fallbackActive; polling logic with 30s interval |
| CREATE | `shared/services/ai-gateway-status.service.ts` | GET /api/v1/ai-gateway/status; polling RxJS stream |
| MODIFY | `app.component.ts` | Call aiStatusFacade.initialize() on ngOnInit |
| MODIFY | `modules/clinical-intelligence/components/coding-suggestion-panel/coding-suggestion-panel.component.ts` | Add @if(fallbackActive) AiFallbackBannerComponent at top |
| MODIFY | `modules/clinical-intelligence/components/patient-profile/patient-profile.component.ts` | Add @if(fallbackActive) banner at AI sections |
| MODIFY | `modules/clinical-intelligence/components/clinical-timeline/clinical-timeline.component.ts` | Add @if(fallbackActive) banner at panel top |

---

## External References

- Angular Signals: https://angular.dev/guide/signals
- RxJS interval + takeUntil: https://rxjs.dev/api/index/function/interval
- AIR-005: Fallback to deterministic manual workflows when AI is unavailable
- TR-008: AI orchestration through provider-agnostic gateway with circuit-breaker fallback

---

## Build Commands

```bash
# Install dependencies
npm install

# Build application
ng build --configuration production

# Run unit tests
ng test --watch=false --browsers=ChromeHeadless

# Lint
ng lint

# Serve locally
ng serve
```

---

## Implementation Validation Strategy

- [ ] `AiFallbackBannerComponent` visible on SCR-017 when `GET /api/v1/ai-gateway/status` returns `circuitState: 'open'` (AC-2, Edge Case 2)
- [ ] Banner disappears automatically when status returns `circuitState: 'closed'` without page reload (AC-3)
- [ ] Polling active every 30s while circuit is open; polling stops when circuit closes
- [ ] Banner has `role="status"` and `aria-live="polite"` — screen reader announces circuit-open state
- [ ] Banner visible on `CodingSuggestionPanelComponent`, `PatientProfileComponent`, `ClinicalTimelineComponent` (Edge Case 2)
- [ ] `AiGatewayStatusFacade.initialize()` called on app startup; does not poll when `fallbackActive: false`

---

## Implementation Checklist

- [ ] Define `AiGatewayStatusDto` (`circuitState`, `fallbackActive`, `lastTripAt`)
- [ ] Create `AiGatewayStatusService` with `getStatus()` and 30s polling stream that terminates on circuit close
- [ ] Create `AiGatewayStatusFacade` with `fallbackActive` Signal; call `initialize()` on app startup
- [ ] Create `AiFallbackBannerComponent`: amber `mat-card`; `role="status"`; `aria-live="polite"`; non-dismissable (Edge Case 2, AC-2)
- [ ] Modify `CodingSuggestionPanelComponent`, `PatientProfileComponent`, `ClinicalTimelineComponent` to add `@if(fallbackActive)` banner
- [ ] Register facade and service in root providers; call `initialize()` in `AppComponent.ngOnInit()`
