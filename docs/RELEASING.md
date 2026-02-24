# Release Guide — Creating GitHub Releases

Step-by-step process for releasing the ERP Generic Policy Framework.

---

## Prerequisites

- [ ] All changes merged to `main` / `master`
- [ ] `CHANGELOG.md` updated with the new version
- [ ] Tests pass locally: `dotnet test ErpPolicyFramework.sln`
- [ ] `NUGET_API_KEY` secret configured (Settings → Secrets → Actions)

---

## Semantic Versioning

| Change | Version bump | Example |
|--------|--------------|---------|
| Bug fix, small improvement | **Patch** (x.y.**Z**) | 1.1.0 → 1.1.1 |
| New feature, backward compatible | **Minor** (x.**Y**.0) | 1.1.0 → 1.2.0 |
| Breaking change | **Major** (**X**.0.0) | 1.2.0 → 2.0.0 |

---

## Release Process

### 1. Update CHANGELOG.md

Add the new version entry at the top (below the header):

```markdown
## [1.2.0] - 2025-03-15

### Added
- New feature X

### Changed
- Improvement Y

### Fixed
- Bug fix Z
```

### 2. Update Version in Projects (optional)

If you want the version in `.csproj` to match before release:

```bash
# PolicyFramework.Core and modules
# Update <Version>1.1.0</Version> to 1.2.0 in each .csproj
```

The CD workflow overrides the version from the tag, so this is optional for consistency in source.

### 3. Commit and Push

```bash
git add CHANGELOG.md
git commit -m "chore: prepare release v1.2.0"
git push origin main
```

### 4. Create the GitHub Release

1. Go to **Releases** → [Create a new release](https://github.com/hakimsameh/ErpPolicyFramework/releases/new)

2. **Choose a tag:** Type `v1.2.0` (or click "Find or create a tag")
   - If the tag does not exist, select **"Create new tag: v1.2.0 on publish"**
   - Target: `main` (or the branch with your release commit)

3. **Release title:** `v1.2.0`

4. **Description:** Copy the relevant section from `CHANGELOG.md`:

   ```markdown
   ## [1.2.0] - 2025-03-15

   ### Added
   - New feature X

   ### Changed
   - Improvement Y
   ```

5. Optionally attach files (NuGet packages are attached automatically by the CD workflow).

6. Uncheck **"Set as the latest release"** for pre-releases.

7. Click **Publish release**.

### 5. Verify

- **Actions** tab: CD workflow runs (Build, Test, Pack, Publish NuGet, Attach to Release)
- **Release page**: NuGet `.nupkg` files appear under Assets
- **NuGet.org**: Packages available after a few minutes

---

## What Happens Automatically

| Step | Triggered by | Result |
|------|--------------|--------|
| Build & Test | Release published | Compile, run tests |
| Pack | After tests pass | Create `.nupkg` for Core + modules |
| Publish to NuGet.org | If `NUGET_API_KEY` set | Packages on nuget.org |
| Attach to Release | Release published | `.nupkg` files on release page |

---

## Alternative: Tag-Only Release

If you prefer not to use the GitHub Release UI:

```bash
git tag v1.2.0
git push origin v1.2.0
```

- CD runs: build, test, pack, publish to NuGet.org
- **Does not** create a GitHub Release or attach artifacts
- Use when you only need NuGet publish, not a visible release

---

## Troubleshooting

| Issue | Fix |
|-------|-----|
| Release created but no packages attached | Ensure you used "Create a new release" (not just push tag). The attach job runs only on `release: published`. |
| Duplicate package on NuGet | Use a new version. Workflow uses `--skip-duplicate` so it will not fail. |
| CD failed: tests | Fix failing tests and push a new commit, then create a new release with an incremented version. |

---

## Checklist Summary

- [ ] Update CHANGELOG.md
- [ ] Commit and push
- [ ] Create release on GitHub (tag `vX.Y.Z`, copy changelog to description)
- [ ] Publish release
- [ ] Verify CD workflow and NuGet.org
