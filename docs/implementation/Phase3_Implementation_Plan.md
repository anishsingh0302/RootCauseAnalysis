# Phase 3 Implementation Plan - Semantic Search & Pattern Recognition

**Phase:** 3
**Duration:** 10-12 days (50-60 hours)
**Status:** In Progress
**Start Date:** 2026-04-08

---

## Overview

Phase 3 adds intelligent failure pattern detection using AI embeddings and vector similarity search. This enables the system to:
- Find similar historical failures
- Identify recurring patterns
- Suggest solutions based on past resolutions
- Proactive failure prediction

---

## Goals

### Primary Goals
1. ✅ Generate embeddings for failure analyses
2. ✅ Implement vector similarity search
3. ✅ Build pattern recognition system
4. ✅ Create search and pattern API endpoints
5. ✅ Comprehensive testing and documentation

### Success Criteria
- Find similar failures with >80% accuracy
- Search response time < 500ms for 1000+ records
- Pattern detection identifies meaningful clusters
- All new endpoints tested and documented
- 40+ integration tests passing

---

## Architecture Decisions

### Embedding Generation
**Choice:** AWS Bedrock Titan Embeddings V2
- **Why:** Already using AWS Bedrock for Claude
- **Model:** amazon.titan-embed-text-v2:0
- **Dimensions:** 1024 or 512 (configurable)
- **Cost:** ~$0.0001 per 1K tokens (minimal)

**Alternative considered:** OpenAI Embeddings (requires separate API key)

### Vector Search
**Choice:** In-Memory Cosine Similarity (Phase 3.1)
- **Why:** Simple, no additional infrastructure, fast for <10K records
- **Library:** Custom implementation or MathNet.Numerics
- **Upgrade Path:** Migrate to Qdrant/pgvector if needed (Phase 3.2)

**Alternative considered:** Qdrant, FAISS, pgvector (more complex setup)

### Clustering Algorithm
**Choice:** K-means with automatic K selection
- **Why:** Simple, well-understood, fast
- **Library:** Accord.NET or custom implementation
- **Parameters:** Auto-determine optimal K using elbow method

**Alternative considered:** DBSCAN, HDBSCAN (more complex but better for varying densities)

---

## Implementation Phases

### Phase 3.1: Core Functionality (Days 1-10)
1. Embedding generation
2. In-memory vector search
3. Basic pattern recognition
4. API endpoints
5. Testing

### Phase 3.2: Enhancements (Future - Optional)
1. Migrate to vector database (Qdrant/pgvector)
2. Advanced clustering algorithms
3. Visualization endpoints
4. Real-time pattern detection

**Note:** Phase 3.2 is optional and can be done later based on production needs.

---

## Day-by-Day Plan

### Day 1-2: Embedding Generation (12-18 hours)

#### Day 1 Morning (3-4 hours)
- [x] Create Phase 3 implementation plan
- [ ] Research AWS Bedrock Titan Embeddings API
- [ ] Create `IEmbeddingService` interface
- [ ] Create `TitanEmbeddingService` implementation
- [ ] Add embedding configuration to appsettings.json
- [ ] Register service in DI container

#### Day 1 Afternoon (2-3 hours)
- [ ] Test embedding generation with sample text
- [ ] Add embedding field population to save workflow
- [ ] Update `FailureRepository.SaveFailureAsync()`
- [ ] Create integration tests for embedding generation

#### Day 2 Morning (3-4 hours)
- [ ] Create batch embedding generator
- [ ] Generate embeddings for existing failures
- [ ] Add progress tracking for batch operations
- [ ] Verify embeddings stored correctly in database

#### Day 2 Afternoon (2-3 hours)
- [ ] Add error handling for embedding failures
- [ ] Add retry logic for API calls
- [ ] Create embedding service tests
- [ ] Document embedding generation

---

### Day 3-5: Vector Search (15-22 hours)

#### Day 3 Morning (3-4 hours)
- [ ] Create `ISimilarityService` interface
- [ ] Implement cosine similarity calculation
- [ ] Create `VectorSearchService` implementation
- [ ] Add similarity threshold configuration

#### Day 3 Afternoon (3-4 hours)
- [ ] Create search request/response models
- [ ] Build similarity search logic
- [ ] Test with sample embeddings
- [ ] Verify similarity scoring

#### Day 4 Morning (3-4 hours)
- [ ] Create `POST /api/pipeline/search/similar` endpoint
- [ ] Implement search by ID (find similar to specific failure)
- [ ] Implement search by text (find similar to new description)
- [ ] Add pagination to search results

#### Day 4 Afternoon (2-3 hours)
- [ ] Add filtering options (error type, date range)
- [ ] Implement ranking by similarity score
- [ ] Add response metadata (search time, total matches)
- [ ] Test endpoint via Swagger UI

#### Day 5 (4-6 hours)
- [ ] Create integration tests for similarity search
- [ ] Performance testing (1000+ records)
- [ ] Edge case testing (no similar failures, all similar)
- [ ] Optimize search performance if needed
- [ ] Document search API

---

### Day 6-8: Pattern Recognition (12-18 hours)

#### Day 6 Morning (3-4 hours)
- [ ] Create `IPatternService` interface
- [ ] Implement K-means clustering
- [ ] Auto-determine optimal K using elbow method
- [ ] Create pattern detection logic

#### Day 6 Afternoon (3-4 hours)
- [ ] Create pattern models (Pattern, PatternCluster)
- [ ] Implement pattern grouping
- [ ] Generate pattern summaries using Claude
- [ ] Test clustering with sample data

#### Day 7 Morning (3-4 hours)
- [ ] Create `GET /api/pipeline/patterns` endpoint
- [ ] Return identified patterns with metadata
- [ ] Add filtering by error type and date range
- [ ] Include cluster statistics

#### Day 7 Afternoon (2-3 hours)
- [ ] Create `GET /api/pipeline/patterns/{id}` endpoint
- [ ] Return failures within specific pattern
- [ ] Add pattern trend analysis
- [ ] Test endpoints via Swagger UI

#### Day 8 (3-4 hours)
- [ ] Create integration tests for pattern recognition
- [ ] Test clustering accuracy
- [ ] Verify pattern summaries are meaningful
- [ ] Document pattern API

---

### Day 9-10: Testing & Documentation (8-12 hours)

#### Day 9 Morning (3-4 hours)
- [ ] Edge case tests for embeddings
- [ ] Edge case tests for search
- [ ] Edge case tests for patterns
- [ ] Performance benchmarking

#### Day 9 Afternoon (2-3 hours)
- [ ] Create comprehensive test report
- [ ] Document all new endpoints
- [ ] Update usage examples
- [ ] Create Phase 3 API guide

#### Day 10 (3-5 hours)
- [ ] User validation via Swagger UI
- [ ] Fix any issues found
- [ ] Create Phase 3 completion report
- [ ] Update TOMORROW_START_HERE.md

---

## Technical Specifications

### New Interfaces

#### IEmbeddingService
```csharp
public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default);
}
```

#### ISimilarityService
```csharp
public interface ISimilarityService
{
    double CalculateCosineSimilarity(float[] embedding1, float[] embedding2);
    Task<List<SimilarityResult>> FindSimilarAsync(float[] queryEmbedding, int limit = 10, double threshold = 0.7, CancellationToken cancellationToken = default);
    Task<List<SimilarityResult>> FindSimilarToFailureAsync(int failureId, int limit = 10, double threshold = 0.7, CancellationToken cancellationToken = default);
}
```

#### IPatternService
```csharp
public interface IPatternService
{
    Task<List<FailurePattern>> DetectPatternsAsync(int minClusterSize = 3, CancellationToken cancellationToken = default);
    Task<FailurePattern?> GetPatternDetailsAsync(int patternId, CancellationToken cancellationToken = default);
    Task<List<FailureEntity>> GetFailuresInPatternAsync(int patternId, CancellationToken cancellationToken = default);
}
```

### New Models

#### SimilarityResult
```csharp
public record SimilarityResult(
    int FailureId,
    string RootCause,
    string ErrorType,
    double SimilarityScore,
    DateTime AnalyzedAt);
```

#### FailurePattern
```csharp
public record FailurePattern(
    int PatternId,
    string PatternName,
    string Description,
    int FailureCount,
    double AverageConfidence,
    List<string> CommonComponents,
    List<string> ErrorTypes,
    DateTime FirstOccurrence,
    DateTime LastOccurrence);
```

### New API Endpoints

#### POST /api/pipeline/search/similar
**Request:**
```json
{
  "failureId": 123,  // Search similar to this failure
  "text": "optional text to search",  // Or search by text
  "limit": 10,
  "threshold": 0.7,
  "errorType": "NullReferenceException"  // optional filter
}
```

**Response:**
```json
{
  "results": [
    {
      "failureId": 125,
      "rootCause": "...",
      "errorType": "NullReferenceException",
      "similarityScore": 0.92,
      "analyzedAt": "2026-04-07T10:30:00Z"
    }
  ],
  "totalMatches": 5,
  "searchTimeMs": 45
}
```

#### GET /api/pipeline/patterns
**Response:**
```json
{
  "patterns": [
    {
      "patternId": 1,
      "patternName": "Payment Service Null Reference Pattern",
      "description": "Common pattern of null references in payment processing",
      "failureCount": 15,
      "averageConfidence": 0.88,
      "commonComponents": ["PaymentService", "PaymentProcessor"],
      "errorTypes": ["NullReferenceException"],
      "firstOccurrence": "2026-03-15T08:00:00Z",
      "lastOccurrence": "2026-04-07T15:30:00Z"
    }
  ],
  "totalPatterns": 3
}
```

#### GET /api/pipeline/patterns/{id}
**Response:**
```json
{
  "pattern": {
    "patternId": 1,
    "patternName": "...",
    "description": "...",
    "failureCount": 15
  },
  "failures": [
    // List of failures in this pattern
  ],
  "recommendations": [
    "Common fix: Add null check before accessing payment object",
    "Review initialization of PaymentProcessor in startup"
  ]
}
```

---

## Database Changes

### No Schema Changes Needed! ✅
The `Embedding` field already exists in `FailureEntity` from Phase 2:
```csharp
public byte[]? Embedding { get; set; }
```

**Storage:**
- Store as byte array (serialize float[] to bytes)
- Dimension: 512 or 1024 (based on Titan model choice)
- Nullable: Yes (for backward compatibility)

---

## Configuration

### appsettings.json Updates

```json
{
  "Embedding": {
    "ModelId": "amazon.titan-embed-text-v2:0",
    "Dimensions": 512,
    "MaxInputLength": 8192
  },
  "Similarity": {
    "DefaultThreshold": 0.7,
    "MaxResults": 100,
    "EnableCaching": true
  },
  "Patterns": {
    "MinClusterSize": 3,
    "MaxClusters": 20,
    "AutoRefreshMinutes": 60
  }
}
```

---

## Testing Strategy

### Integration Tests (Target: 40+ tests)

#### Embedding Tests (~8 tests)
- Generate single embedding
- Generate batch embeddings
- Handle empty text
- Handle very long text
- Verify dimensions correct
- Test retry logic
- Test error handling
- Performance benchmarking

#### Similarity Search Tests (~12 tests)
- Find similar by ID
- Find similar by text
- Verify similarity scores
- Test threshold filtering
- Test pagination
- Test with no matches
- Test with all matches
- Performance with 1000+ records
- Edge cases (null embeddings, single record)

#### Pattern Recognition Tests (~10 tests)
- Detect patterns with known data
- Verify cluster quality
- Test pattern summaries
- Test pattern retrieval
- Test failures in pattern
- Edge cases (no patterns, single cluster)
- Performance benchmarking

#### API Integration Tests (~10 tests)
- Test all new endpoints
- Test error responses
- Test pagination
- Test filtering
- Verify response formats

---

## Performance Targets

### Embedding Generation
- **Single:** < 2 seconds
- **Batch (100):** < 30 seconds
- **Retry logic:** Max 3 attempts

### Similarity Search
- **< 1000 records:** < 100ms
- **1000-5000 records:** < 300ms
- **> 5000 records:** < 500ms

### Pattern Detection
- **< 1000 records:** < 2 seconds
- **1000-5000 records:** < 5 seconds
- **Cached results:** < 50ms

---

## Known Limitations

### Embedding Generation
- Requires AWS credentials
- Rate limits: ~10 requests/second
- Cost: Minimal but not zero

### Vector Search
- In-memory: Limited to ~10K records efficiently
- No real-time updates (requires restart for new embeddings)
- Single-threaded search

### Patterns
- K-means requires pre-defined K (using auto-detection)
- Patterns may shift as new data added
- Requires periodic recalculation

---

## Upgrade Path (Phase 3.2 - Optional)

### When to Upgrade
- More than 10,000 failures
- Need real-time pattern updates
- Require sub-100ms search times
- Multiple concurrent search requests

### Migration Steps
1. Deploy Qdrant or PostgreSQL with pgvector
2. Migrate embeddings to vector DB
3. Update `ISimilarityService` implementation
4. No API changes needed (same interface)
5. Performance testing and tuning

---

## Risk Mitigation

### Risk: Embedding API failures
**Mitigation:**
- Retry logic with exponential backoff
- Queue failed embeddings for later processing
- Continue saving failures even if embedding fails

### Risk: Poor similarity results
**Mitigation:**
- Tunable threshold parameter
- Multiple similarity metrics (cosine, euclidean)
- User feedback mechanism

### Risk: Performance degradation
**Mitigation:**
- Implement caching for common searches
- Background processing for pattern detection
- Pagination for large result sets

---

## Success Metrics

### Functional
- [ ] 100% of failures get embeddings generated
- [ ] Similarity search returns relevant results
- [ ] Patterns are meaningful and actionable
- [ ] All API endpoints working

### Performance
- [ ] Embedding generation < 2s per failure
- [ ] Search response time < 500ms
- [ ] Pattern detection < 5s
- [ ] All tests passing (40+)

### Quality
- [ ] Code coverage > 80%
- [ ] Documentation complete
- [ ] User validation successful
- [ ] Production ready

---

## Dependencies

### NuGet Packages Needed
- AWSSDK.BedrockRuntime (already have)
- MathNet.Numerics (for vector operations) - OR custom implementation
- Accord.NET (for clustering) - OR custom implementation

**Decision:** Start with custom implementation to minimize dependencies

---

## Documentation Deliverables

1. [ ] Phase 3 API Guide (search & patterns)
2. [ ] Embedding Generation Guide
3. [ ] Pattern Recognition Guide
4. [ ] Test Results Report (Phase 3)
5. [ ] Phase 3 Completion Report
6. [ ] Updated Usage Examples

---

## Next Steps

**Start:** Day 1 - Embedding Generation
**First Task:** Research AWS Bedrock Titan Embeddings API and create `IEmbeddingService` interface

---

**Plan Created:** 2026-04-08
**Status:** Ready to Begin
**Estimated Completion:** 2026-04-18 to 2026-04-20
