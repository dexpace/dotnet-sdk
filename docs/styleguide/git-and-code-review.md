# Git and Code Review

Cross-cutting rules for version control, commit discipline, and code review.

## Commit Messages

- **Imperative mood.** "Add feature" not "Added feature." "Fix crash" not "Fixes crash."
- First line under **72 characters**. This is the subject.
- Blank line after subject, then body explaining **why** -- the diff shows what. Context the reviewer and future-you need.
- Reference issue/ticket numbers: `Closes #123`, `Refs EG-4567`.
- **Conventional Commits** format:

```
type(scope): description

Body explaining why this change is necessary.
What problem does it solve? What trade-offs were made?

Refs #123
```

Types: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`, `perf`, `ci`, `build`.

```
feat(search): add hotel availability filtering

The search endpoint returned all hotels regardless of availability,
forcing clients to filter client-side. This adds server-side filtering
by availability date range, reducing response payload by ~60%.

Closes #456
```

## Commit Granularity

- **One logical change per commit.** A commit is an atomic unit of work.
- Do not mix refactoring with feature work. Do not mix formatting with logic changes. Do not mix dependency updates with code changes.
- Each commit must compile and pass tests. Bisecting broken commits wastes everyone's time.
- If you find a bug while working on a feature, fix it in a separate commit before the feature commit.
- Prefer many small commits over few large commits. Small commits are easier to review, revert, and bisect.

## Branch Naming

- Format: `type/short-description`. Lowercase. Hyphens for separators. No underscores.

```
feat/add-hotel-search
fix/null-pointer-in-auth
refactor/extract-http-pipeline
test/add-booking-integration-tests
chore/upgrade-deps
```

- Keep branch names under 50 characters. They appear in merge commits and logs.
- Delete branches after merging. Stale branches are clutter.

## PR Conventions

- **Title under 72 characters.** Same imperative mood as commit messages.
- Description must include:
  - **Summary**: What changed and why. Not a restatement of the diff -- the motivation and context.
  - **Test plan**: How you verified the change works. Manual steps, test output, screenshots if UI.
  - **Breaking changes**: If any, describe the migration path.

```markdown
## Summary
- Add server-side availability filtering to hotel search endpoint
- Reduces response payload by ~60% for date-bounded queries

## Test plan
- Added integration tests for filtered and unfiltered queries
- Load tested with 10k concurrent requests, p99 latency unchanged
- Verified backward compatibility: omitting date params returns all hotels

## Breaking changes
None
```

- **Small PRs.** Under 400 lines of real code (excluding generated code, tests, config). Large PRs get rubber-stamped, not reviewed.
- Split large changes into **stacked PRs** with clear dependency order. Each PR is reviewable and mergeable independently.

## Code Review Checklist

Review in this order:

1. **Correctness.** Does it do what it claims? Are edge cases handled?
2. **Error handling.** Every error path covered? Assertions present? Fail-fast on invalid state?
3. **Tests.** Do tests cover the change? Are they testing behavior, not implementation?
4. **Naming.** Are names clear and precise? Could a reader understand the code without comments?
5. **Abstractions.** Are they necessary? Is there premature generalization? Simpler is better.
6. **Security.** Leaked secrets? Injection vectors? Unvalidated input?
7. **Performance.** N+1 queries? Unbounded allocations? Missing timeouts?
8. **Compatibility.** Backward compatible? Breaking changes documented?

## Review Etiquette

- Prefix comments with severity:
  - `nit:` -- optional, stylistic, take it or leave it
  - `suggestion:` -- worth considering, not blocking
  - `issue:` -- must fix before merge
  - `question:` -- need to understand before approving

```
nit: rename `proc` to `processBooking` for clarity

issue: this query concatenates user input -- use parameterized query

question: why does this retry 5 times? is there a specific failure mode?

suggestion: consider extracting this into a shared utility, same pattern in BookingService
```

- **Approve with nits.** Don't block a PR on style preferences. Ship it, fix nits in a follow-up if the author agrees.
- Review within 24 hours. Stale PRs rot. If you can't review in time, say so and suggest another reviewer.
- Be specific. "This is confusing" is not actionable. "Rename `x` to `maxRetryCount` -- I had to read the function body to understand what it controls" is.

## Merge Strategy

- **Squash merge** for feature branches. One clean commit in main per feature. Keeps history readable.
- **Rebase** for keeping linear history when multiple commits are individually meaningful (e.g., stacked refactoring steps).
- **Never force push to main or shared branches.** Force pushing rewrites history that others depend on.
- Ensure CI passes before merging. No "it works on my machine" merges.
- Delete the source branch after merge.

## Release Discipline

- **Tag releases.** Every release gets a git tag: `v1.2.3`.
- **Semantic versioning.** `MAJOR.MINOR.PATCH`:
  - MAJOR: breaking changes
  - MINOR: new features, backward compatible
  - PATCH: bug fixes, backward compatible
- **Breaking changes require a major version bump.** No exceptions. Document the migration path in release notes.
- Write release notes that explain what changed from the user's perspective. Link to relevant PRs and issues.
- Never release on Fridays. Never release without a rollback plan.
