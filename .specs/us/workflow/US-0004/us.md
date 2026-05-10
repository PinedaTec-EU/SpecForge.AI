# US-0004 · Synchronize client prompts and rules from Central

## Metadata
- Kind: `feature`
- Category: `workflow`

## Objective
## SpecForge Goal Intake

- Goal: `GOAL-SPECFORGE-CENTRAL`
- Strategy: `small-user-stories`
- Sequence: `4` of `8`
- Coding policy: do not implement directly from the broad goal; drive this story through SpecForge SDD phases before code changes.

## Original Goal

Develop SpecForge Central as an optional central service for teams using SpecForge clients. Central keeps user stories, workflow states, prompts and rules aligned across developers, supports versioned centralized configuration distribution, lets administrators create and assign user stories, lets client-created user stories sync back and become assigned to the creating developer, protects central configuration with a password or equivalent authorization so developers cannot bypass company rules, and supports offline client work with later synchronization.

## User Story Slice

As a connected SpecForge developer, I want my client to download the active Central configuration release and apply prompts or rules needed to reach the company-approved level so that my local workflow follows the same standards as the rest of the team.

## MVP Slice

- Outcome: Connected clients can converge to the active Central prompts and rules.
- Slice rationale: This is one small independently reviewable increment for SpecForge Central.

## Acceptance Intent

- The client checks Central for the active configuration release when online.
- When the client is behind, it downloads the required prompt and rule artifacts and records the applied version locally.
- A failed download leaves the previous local configuration usable and reports a clear sync status.

## Non Goals

- Allowing developers to selectively opt out of mandatory Central rules.
- Implementing advanced merge of manually edited local prompt files.

## Dependencies

- US-0001
- US-0003

## Initial Scope
- Includes:
  - ...
- Excludes:
  - ...
