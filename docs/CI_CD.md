# CI/CD Setup

## Add NuGet API Key (Required for publishing)

1. **Regenerate your NuGet API key** (if you've ever shared it):
   - Go to [nuget.org/account/apikeys](https://www.nuget.org/account/apikeys)
   - Delete the old key and create a new one

2. **Add to GitHub Secrets:**
   - Open [github.com/hakimsameh/ErpPolicyFramework](https://github.com/hakimsameh/ErpPolicyFramework)
   - **Settings** → **Secrets and variables** → **Actions**
   - **New repository secret**
   - Name: `NUGET_API_KEY`
   - Value: *(paste your new NuGet API key)*
   - **Add secret**

## Triggers

| Workflow | Trigger | What happens |
|----------|---------|--------------|
| **CI** | Push or PR to `main` / `master` | Build + test (does **not** publish) |
| **CD** | Push tag `v*` (e.g. `v1.0.0`) **or** publish a Release | Build, test, pack, upload artifacts; publish to NuGet.org when `NUGET_API_KEY` is set |

**Important:** Pushing commits to `main` runs **CI only**, not CD. To publish to NuGet, you must push a version tag or create a Release.

## Publish PolicyFramework.Core to NuGet.org

**Option A: Tag only**

```bash
git tag v1.0.0
git push origin v1.0.0
```

CD runs → packs → publishes to NuGet.org (if `NUGET_API_KEY` is set).

**Option B: GitHub Release (recommended)**

1. Update [CHANGELOG.md](../CHANGELOG.md) with the new version
2. Commit, push to `main`
3. **Releases** → **Create a new release**
4. Tag: `v1.1.0` (or create new tag on publish)
5. Description: copy the version section from CHANGELOG.md
6. **Publish release**

CD runs → build, test, pack → publishes to NuGet.org → attaches `.nupkg` files to the release.

Full process: [docs/RELEASING.md](RELEASING.md)

---

## Troubleshooting: NuGet Not Publishing

| Symptom | Cause | Fix |
|---------|-------|-----|
| Publish job is skipped | CD only runs on tag push (`v*`) or Release publish | Push a tag: `git tag v1.0.0 && git push origin v1.0.0` or create a GitHub Release |
| "NUGET_API_KEY secret is not set" | Secret missing or misnamed | Add `NUGET_API_KEY` in **Settings → Secrets and variables → Actions** |
| `dotnet nuget push` fails with 403 | Invalid or expired API key | Regenerate key at [nuget.org/account/apikeys](https://www.nuget.org/account/apikeys), update the secret |
| Duplicate package version | Same version already on NuGet.org | Use a new version (e.g. `v1.0.1`); or workflow uses `--skip-duplicate` so it will not fail |
