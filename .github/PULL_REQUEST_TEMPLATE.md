<!--
Thanks for the PR. Please fill in the sections below so reviewers can move quickly.
Delete sections that don't apply.
-->

## Summary

<!-- One or two sentences describing the change and why it's needed. -->

## Related issue / discussion

Closes #
Refs #

## Type of change

- [ ] Bug fix (non-breaking)
- [ ] New feature (non-breaking)
- [ ] Breaking change (behavior, response shape, or config key changed)
- [ ] Provider integration
- [ ] Domain agent
- [ ] Skill
- [ ] Docs only
- [ ] Build / CI / chore

## What changed

<!--
Walk reviewers through the diff at a high level. Mention:
- Which layer each change touches (Core / Features / Infrastructure / Skills / Program.cs).
- New or modified contracts (interfaces, records).
- Decorator order changes in Program.cs.
-->

## How to test

```bash
# Repro command(s) and expected output
curl -X POST http://localhost:5042/api/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{"prompt":"..."}'
```

## Checklist

- [ ] `dotnet build` succeeds with **0 warnings**.
- [ ] I ran the change against at least one real provider (or explained why I couldn't).
- [ ] I updated relevant docs (`README.md`, `docs/*`, agent / skill guides).
- [ ] I added an entry to `CHANGELOG.md` under `## [Unreleased]`.
- [ ] My commits follow [Conventional Commits](https://www.conventionalcommits.org/).
- [ ] I have read [CONTRIBUTING.md](../CONTRIBUTING.md).

## Screenshots / logs (optional)

<!-- Drop response examples, log excerpts, or before/after diffs here. -->
