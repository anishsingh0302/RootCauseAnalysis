# Phase 7: Iterative Retry on Same Branch - Design Document

## Problem Statement

Current behavior (problematic):
- Multiple commits happening on different branches
- Multiple PRs being created for the same failure
- Each attempt creates a new branch
- No context accumulation from previous failures

Expected behavior:
1. Build fails (manually triggered or webhook)
2. Analyze root cause and commit fix to branch X (e.g., autofix/session-123)
3. Pipeline runs automatically on branch X
4. If pipeline succeeds on branch X → Create PR
5. If pipeline fails on branch X → Analyze NEW failure + keep context of WHY last fix failed
6. Commit new fix to SAME branch X (not a new branch)
7. Pipeline runs again on branch X
8. Repeat until success OR max retries (3 attempts)
9. Only create PR after pipeline succeeds

## Architecture

### New Components

1. **AutoFixSession** (Model)
   - Tracks single auto-fix session
   - Stores branch name, retry count, failure history
   - Status: InProgress, Succeeded, Failed, MaxRetriesReached

2. **AutoFixSessionManager** (Service)
   - Manages active sessions in memory
   - Creates new sessions
   - Records failures and updates context
   - Builds accumulated context from history

3. **IterativeAutoFixOrchestrator** (Service)
   - Coordinates session lifecycle
   - StartNewSessionAsync() - creates branch and commits first fix
   - RetryOnExistingBranchAsync() - commits additional fixes to same branch
   - HandleSuccessfulPipelineAsync() - creates PR after success

4. **Modified WebhookController**
   - Detects autofix branch failures
   - Routes to orchestrator based on branch type
   - Handles both failure and success events

### Flow Diagram

```
Initial Failure (master/main):
  Webhook (failure) → ProcessWorkflowFailureAsync
    → Check: Is autofix branch? NO
    → IterativeAutoFixOrchestrator.StartNewSessionAsync()
      → Create Session
      → Generate Fix with Validation
      → Create Branch "autofix/session-20260427-123456"
      → Commit Fix #1
      → DO NOT CREATE PR YET
      → Wait for pipeline result

Pipeline Runs on autofix/session-20260427-123456:
  Case 1 - Pipeline Succeeds:
    Webhook (success) → ProcessWorkflowSuccessAsync
      → Check: Is autofix branch? YES
      → IterativeAutoFixOrchestrator.HandleSuccessfulPipelineAsync()
        → Create PR with session history
        → Mark session as succeeded

  Case 2 - Pipeline Fails:
    Webhook (failure) → ProcessWorkflowFailureAsync
      → Check: Is autofix branch? YES
      → Check: Can retry? (attempt < 3)
      → IterativeAutoFixOrchestrator.RetryOnExistingBranchAsync()
        → Build accumulated context (all previous failures)
        → Generate NEW fix with context
        → Commit Fix #2 to SAME branch
        → Update session history
        → Wait for pipeline result
      → Loop back to "Pipeline Runs"
```

## Implementation Details

### Session Management

Sessions stored in-memory using ConcurrentDictionary:
- Key: Branch name
- Value: AutoFixSession object

Branch naming convention:
```
autofix/session-{timestamp}
Example: autofix/session-20260427-142530
```

### Context Accumulation

Each retry includes history:
```
## Auto-Fix Session History
Session ID: abc123
Current Attempt: 2/3

## Previous Fix Attempts and Failures:

### Attempt 1
Error Type: TestFailure
Root Cause: Assert.Equal expected 20 but was 21
Fix Attempted: Changed expected value to 21
Why It Failed: New error - NullReferenceException in Setup
Files Modified: CustomerTests.cs

CRITICAL INSTRUCTIONS:
- DO NOT repeat the same fix that already failed
- Analyze WHY previous fixes failed and try a different approach
```

### Webhook Changes

Current webhooks only handle failures. Need to handle SUCCESS too:

```csharp
if (payload.Action == "completed" && payload.WorkflowRun?.Conclusion == "success")
{
    // Check if this is an autofix branch success
    await ProcessWorkflowSuccessAsync(payload, serviceProvider);
}
else if (payload.Action == "completed" && payload.WorkflowRun?.Conclusion == "failure")
{
    // Existing failure logic
    await ProcessWorkflowFailureAsync(payload, serviceProvider);
}
```

### Configuration

Add to AutoFixSettings:
```json
{
  "EnableIterativeRetry": true,
  "MaxBranchRetries": 3,
  "SessionCleanupHours": 24
}
```

### Safety Features

1. **Max Retries**: Hard limit of 3 attempts per session
2. **Session Timeout**: Auto-cleanup after 24 hours of inactivity
3. **Context Size Limit**: Prevent context from growing too large
4. **Duplicate Prevention**: One session per branch name

## Files Created/Modified

### New Files:
1. AutoFixSession.cs - Session model
2. AutoFixSessionManager.cs - Session management
3. IterativeAutoFixOrchestrator.cs - Orchestration logic

### Modified Files:
1. IAutoFixService.cs - Added CommitToExistingBranchAsync, CreatePRForBranchAsync
2. AutoFixService.cs - Implemented new methods
3. WebhookController.cs - Added success handling, routed to orchestrator
4. Program.cs - Registered new services
5. appsettings.json - Added session configuration

## Testing Plan

1. **Initial Failure Test**
   - Trigger failing build on master
   - Verify branch created (autofix/session-xxx)
   - Verify fix committed
   - Verify NO PR created yet

2. **Success After First Attempt**
   - Fix succeeds on first try
   - Verify PR created
   - Verify PR body shows "1 attempt"

3. **Retry Test**
   - First fix fails
   - Verify second commit on SAME branch
   - Verify accumulated context included
   - Verify NO new branch created

4. **Success After Retry**
   - Second fix succeeds
   - Verify PR created
   - Verify PR body shows "2 attempts" with history

5. **Max Retries Test**
   - All 3 attempts fail
   - Verify session marked as failed
   - Verify no more attempts made
   - Verify notification sent to user

6. **Concurrent Sessions**
   - Multiple failures on different repos
   - Verify separate sessions maintained
   - Verify no cross-contamination

## Migration from Old System

Old system immediately creates PRs. New system waits for success.

**Breaking Change:** PRs will no longer be created immediately.

**Migration Steps:**
1. Deploy new code with EnableIterativeRetry=false (old behavior)
2. Test in staging
3. Enable EnableIterativeRetry=true
4. Monitor first few sessions
5. Clean up any orphaned branches from old system

## Success Metrics

Before:
- 10+ branches per failure
- 10+ PRs per failure
- 0% success rate

After:
- 1 branch per failure
- 1 PR per successful fix
- 90%+ confidence before PR creation
- Context accumulation improves success rate

## Future Enhancements

1. Persist sessions to database (currently in-memory)
2. Add UI to view active sessions
3. Manual intervention API (abandon session, force PR, etc.)
4. Machine learning on session outcomes
5. Parallel fix attempts (generate multiple fixes, apply best one)
