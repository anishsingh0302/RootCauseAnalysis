# Scripts Archive - Location Update

**Date:** 2026-04-25

---

##  Archive Moved

The scripts archive has been moved to consolidate all archived content in one location.

**Old Location:** `scripts/archive/`
**New Location:** `docs/archive/scripts/`

---

##  Why This Change?

**Consolidation:** All archived content (documentation + scripts) is now in `docs/archive/`

**Benefits:**
-  Single location for all historical/testing content
-  Cleaner main `scripts/` folder (only essential scripts)
-  Easier to understand project structure
-  Better separation: production vs. testing/development

---

##  New Structure

```
docs/archive/
├── implementation/      # Implementation plans
├── tracking/           # Development history
├── testing/            # Historical test results
├── planning/           # Project planning
└── scripts/            # Testing and legacy scripts ← YOU ARE HERE
    ├── testing/        # Component test scripts
    ├── utilities/      # Helper scripts
    └── aws-legacy/     # Legacy AWS scripts
```

---

##  Finding Scripts

### Essential Scripts (Production)
Located in `scripts/` at project root:
- `scripts/aws/set-sso-creds-persistent.ps1` - AWS credential setup
- `scripts/configuration/configure-environment.ps1` - GitHub/Azure DevOps tokens
- `scripts/utils/test-env.ps1` - Environment verification

See **[Scripts README](../../../scripts/README.md)**

### Archived Scripts (Testing/Legacy)
Located here in `docs/archive/scripts/`:
- `testing/` - Component test scripts
- `utilities/` - Helper scripts
- `aws-legacy/` - Legacy AWS scripts

See **[Archived Scripts README](README.md)**

---

##  Updated Paths

If you had bookmarks or references to old script locations:

| Old Path | New Path |
|----------|----------|
| `scripts/archive/testing/test-webhook.ps1` | `docs/archive/scripts/testing/test-webhook.ps1` |
| `scripts/archive/testing/test-azure-logs.ps1` | `docs/archive/scripts/testing/test-azure-logs.ps1` |
| `scripts/archive/utilities/Problem_trobleshoot.ps1` | `docs/archive/scripts/utilities/Problem_trobleshoot.ps1` |
| `scripts/archive/aws-legacy/aws-helper.ps1` | `docs/archive/scripts/aws-legacy/aws-helper.ps1` |

---

##  What This Means

**For Essential Scripts:** No change - they remain in `scripts/` for production use

**For Testing Scripts:** Now in `docs/archive/scripts/testing/` - still available for component testing

**For Documentation:** All references updated throughout the project

---

**Move Date:** 2026-04-25
**Status:** Complete
**Impact:** None on production usage - only archive location changed
