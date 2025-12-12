# Applying upstream suggestions to your local branch

If you want to bring the latest committed suggestions (like the Eastern Time helper refactor) onto a fresh local branch, use these steps from the repository root.

1. **Update local refs and create your branch**
   ```bash
   git fetch origin
   git checkout -b <your-branch> origin/main
   ```

2. **Cherry-pick the suggestion commit onto your branch**
   The Eastern Time refactor and related wiring are already committed as `50b7837` on this repo. To apply exactly that commit to your branch:
   ```bash
   git cherry-pick 50b7837
   ```
   If you prefer to verify first, list recent commits and cherry-pick by hash:
   ```bash
   git log --oneline -n 5
   git cherry-pick <suggestion-commit-sha>
   ```
   > If there are conflicts, resolve them, then continue with `git cherry-pick --continue`.

3. **Verify the solution builds locally**
   ```bash
   dotnet restore BetOddsIngestor.sln
   dotnet build BetOddsIngestor.sln
   ```
   Add `dotnet test BetOddsIngestor.sln` once tests are available.

4. **Push your branch and open a PR**
   ```bash
   git push -u origin <your-branch>
   ```

These steps work whether you are starting from `main` or applying the changes to an existing feature branch (switch to that branch in step 1 instead of creating a new one).

## Restoring your repo to the suggestion commit
If you lost your local changes and just want your working tree to match the published suggestion commit (`867ef01` on this branch), you can reset your branch directly to it. **Warning:** `reset --hard` discards uncommitted changes.

```bash
# optional: back up anything uncommitted before resetting
git status
git stash push -m backup-before-restore

# jump back to the exact suggestion commit
git fetch origin
git checkout work           # or your target branch
git reset --hard 867ef01

# confirm the working tree now matches that commit
git status
git log --oneline -n 3
```

If you prefer to keep your current branch state but inspect the files from the suggestion commit, you can check it out into a temporary branch instead:

```bash
git fetch origin
git checkout -b restore-suggestion 867ef01
```
