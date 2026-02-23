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
| **CI** | Push or PR to `main` / `master` | Build + test |
| **CD** | Push tag `v*` (e.g. `v1.0.0`) **or** publish a Release | Build, test, pack, upload artifacts; publish to NuGet.org when `NUGET_API_KEY` is set |

## Publish PolicyFramework.Core to NuGet.org

**Option A: Tag only**

```bash
git tag v1.0.0
git push origin v1.0.0
```

CD runs → packs → publishes to NuGet.org (if `NUGET_API_KEY` is set).

**Option B: GitHub Release**

1. **Releases** → **Create a new release**
2. Tag: `v1.0.0`
3. **Publish release**

CD runs → packs → publishes to NuGet.org → attaches `.nupkg` files to the release.
