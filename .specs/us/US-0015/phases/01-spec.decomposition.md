# Decomposition · US-0015

## State
- State: `approved`
- Decision: `required`
- Complexity score: `0,60`
- Threshold: `0,60`
- Tolerance: `0,10`

## Rationale
The parent spec mixes three independently reviewable concerns: the aggregate-specific workflow layout, parent-child navigation, and the persisted artifact lineage that explains why the split happened. Keeping those concerns in one implementation pass would hide the evidence trail and make local validation ambiguous, so the spec was split into focused child stories.

## Proposed Child User Stories
1. Test: hija 1 - primer slice visual
   - Objective: Validar la primera historia hija generada desde la historia agrupadora de prueba.
   - Acceptance criteria: La hija 1 aparece vinculada a la agregadora y es navegable en el portal.
   - Dependencies: n/a
2. Test: hija 2 - segundo slice visual
   - Objective: Validar la segunda historia hija generada desde la historia agrupadora de prueba.
   - Acceptance criteria: La hija 2 aparece vinculada a la agregadora y muestra el parent correctamente.
   - Dependencies: Test: hija 1 - primer slice visual

## Created Child User Stories
- `US-0016`
- `US-0017`
