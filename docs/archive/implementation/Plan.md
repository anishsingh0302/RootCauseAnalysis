# Agentic Pipeline Troubleshooter - Implementation Plan

## 1. Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                   CI/CD System                           │
│         GitHub Actions / Azure DevOps                    │
└──────────────────────┬──────────────────────────────────┘
                       │ Webhook (on failure)
┌────────────────────────────────────────────────────────┐
│                  API Gateway (FastAPI)                    │
│         REST endpoints + Webhook receiver                │
└──────┬─────────┬──────────┬──────────┬──────────────────┘
       │         │          │          │
┌─────────┐ ┌───────┐ ┌─────┐ ┌─────────────────────┐
│ Log      │ │ Commit │ │ RCA  │ │ Fix Recommendation   │
│ Ingestion│ │ Analyzer│ │Agent │ │ Agent                │
│ Agent    │ │ Agent  │ │      │ │                      │
└──────┬───┘ └───┬────┘ └──┬───┘ └───┬──────────────────┘
       │         │          │          │
┌─────────────────────────────────────────────────────┐
│           LLM Layer (AWS Bedrock - Claude)               │
│            LangChain / LangGraph Orchestration            │
└──────────────────────┬──────────────────────────────────┘
                       │
                       ├──> GitHub PR Comment (PyGithub)
                       ├──> Slack/Teams Notification (optional)
                       │
┌────────────────────────────────────────────────────────┐
│              Data Store (SQLite / PostgreSQL)             │
│       Pipeline logs, history, embeddings (FAISS)         │
└─────────────────────────────────────────────────────────┘
```

---

## 2. Tech Stack

| Layer              | Technology                              |
|--------------------|-----------------------------------------|
| Language           | Python 3.11+                            |
| LLM Orchestration  | LangChain + LangGraph                  |
| LLM Provider       | AWS Bedrock (Anthropic Claude)         |
| Vector Store       | FAISS (local) or ChromaDB              |
| Backend API        | FastAPI                                 |
| Database           | SQLite (MVP) / PostgreSQL (production) |
| CI/CD Integration  | GitHub Actions API, Azure DevOps API   |
| Output/Integration | GitHub PR Comments (PyGithub)          |
| Containerization   | Docker + Docker Compose                |

---

## 3. Module Breakdown

### Module 1: Log Ingestion Agent
**Purpose:** Collect and parse pipeline logs from CI/CD systems.

- Connect to GitHub Actions / Azure DevOps / Jenkins APIs
- Fetch failed pipeline run logs
- Parse and normalize log formats into structured data
- Store raw and parsed logs in the database
- Extract error messages, stack traces, and failure stages

**Key files:**
- `agents/log_ingestion_agent.py`
- `connectors/github_connector.py`
- `connectors/azure_devops_connector.py`
- `parsers/log_parser.py`

### Module 2: Commit Analyzer Agent
**Purpose:** Analyze recent code commits correlated to pipeline failures.

- Fetch recent commits from the repository (git log / API)
- Identify files changed, diff content, and commit messages
- Correlate commit timestamps with pipeline failure times
- Flag suspicious changes (config files, dependency changes, build scripts)

**Key files:**
- `agents/commit_analyzer_agent.py`
- `connectors/git_connector.py`
- `utils/diff_analyzer.py`

### Module 3: Root Cause Analysis (RCA) Agent
**Purpose:** Use LLM to determine the most probable root cause.

- Take parsed logs + commit analysis as input
- Query vector store for similar historical failures
- Prompt LLM with structured context for root cause identification
- Return ranked list of probable causes with confidence scores
- Cross-reference with known failure patterns

**Key files:**
- `agents/rca_agent.py`
- `prompts/rca_prompts.py`
- `knowledge/failure_patterns.py`

### Module 4: Fix Recommendation Agent
**Purpose:** Suggest actionable fixes based on the root cause.

- Generate code-level fix suggestions
- Generate configuration-level fix suggestions
- Reference documentation and known fixes from history
- Format recommendations with file paths and line numbers

**Key files:**
- `agents/fix_recommendation_agent.py`
- `prompts/fix_prompts.py`

### Module 5: Conflict Detection
**Purpose:** Detect inconsistencies between commits and pipeline expectations.

- Compare dependency versions across commits
- Detect environment variable mismatches
- Identify breaking changes in APIs or shared configs
- Flag merge conflict residues

**Key files:**
- `agents/conflict_detection_agent.py`
- `utils/dependency_checker.py`

### Module 6: Pipeline Orchestrator (LangGraph)
**Purpose:** Coordinate all agents in a graph-based workflow.

- Define the agent execution graph (LangGraph state machine)
- Manage state passing between agents
- Handle retries and fallbacks
- Produce final consolidated report

**Key files:**
- `orchestrator/pipeline_graph.py`
- `orchestrator/state.py`

### Module 7: API Layer (FastAPI)
**Purpose:** Expose REST endpoints for triggering analysis and retrieving results.

- `POST /analyze` - Trigger full pipeline analysis (manual via Swagger UI)
- `GET /reports/{id}` - Get analysis report
- `GET /history` - List past analyses
- `POST /webhook` - Receive CI/CD failure webhooks (auto-trigger)

**Key files:**
- `api/main.py`
- `api/routes/analyze.py`
- `api/routes/reports.py`
- `api/routes/webhook.py`
- `api/models/schemas.py`

### Module 8: Output Integrations
**Purpose:** Deliver analysis results where developers already work.

**Features:**
- **GitHub PR Comments:** Post RCA and fix recommendations as PR comments
- **Markdown Reports:** Generate formatted reports saved to database
- **Slack/Teams Integration (Optional):** Send notifications on failure analysis
- **Email Reports (Optional):** Send summary to team

**Key files:**
- `integrations/github_comment_service.py`
- `integrations/slack_service.py` (optional)
- `templates/rca_markdown_template.py`

---

## 4. Project Structure

```
Agentic_Pipeline/
├── agents/
│   ├── __init__.py
│   ├── log_ingestion_agent.py
│   ├── commit_analyzer_agent.py
│   ├── rca_agent.py
│   ├── fix_recommendation_agent.py
│   └── conflict_detection_agent.py
├── orchestrator/
│   ├── __init__.py
│   ├── pipeline_graph.py
│   └── state.py
├── connectors/
│   ├── __init__.py
│   ├── github_connector.py
│   ├── azure_devops_connector.py
│   └── git_connector.py
├── integrations/
│   ├── __init__.py
│   ├── github_comment_service.py
│   └── slack_service.py
├── parsers/
│   ├── __init__.py
│   └── log_parser.py
├── prompts/
│   ├── __init__.py
│   ├── rca_prompts.py
│   └── fix_prompts.py
├── templates/
│   ├── __init__.py
│   └── rca_markdown_template.py
├── knowledge/
│   ├── __init__.py
│   └── failure_patterns.py
├── api/
│   ├── __init__.py
│   ├── main.py
│   ├── routes/
│   │   ├── analyze.py
│   │   ├── reports.py
│   │   └── webhook.py
│   └── models/
│       └── schemas.py
├── utils/
│   ├── __init__.py
│   ├── diff_analyzer.py
│   └── dependency_checker.py
├── data/
│   ├── sample_logs/
│   └── vector_store/
├── tests/
│   ├── test_agents/
│   ├── test_connectors/
│   └── test_api/
├── config.py
├── requirements.txt
├── .env.example
├── Dockerfile
├── docker-compose.yml
├── Plan.md
├── Session.md
└── requirement.md
```

---

## 5. Implementation Phases

### Phase 1: Foundation (MVP Core)
- Set up project structure and dependencies (`requirements.txt` with `boto3`, `langchain-aws`)
- Configure AWS credentials (IAM user with Bedrock access)
- Implement Log Ingestion Agent with sample/mock logs
- Implement basic Log Parser
- Set up LLM integration (AWS Bedrock with Claude via `boto3` or `ChatBedrock`)
- Build RCA Agent with basic prompt engineering
- Create simple CLI interface to test end-to-end flow

### Phase 2: Commit Analysis & Correlation
- Implement Commit Analyzer Agent
- Build Git connector for fetching diffs and history
- Correlate commits with pipeline failures
- Integrate commit context into RCA Agent prompts

### Phase 3: Fix Recommendations & Conflict Detection
- Implement Fix Recommendation Agent
- Implement Conflict Detection Agent
- Add vector store (FAISS) for historical failure matching
- Build knowledge base of common failure patterns

### Phase 4: Orchestration & API
- Build LangGraph orchestrator to coordinate all agents
- Implement FastAPI backend with endpoints
- Add webhook support for CI/CD system integration
- Implement webhook signature verification
- Set up SQLite database for persistence

### Phase 5: Output Integrations & Polish
- Implement GitHub PR comment posting (PyGithub)
- Format RCA as Markdown with syntax highlighting
- Add Slack/Teams webhook integration (optional)
- Configure webhooks in GitHub Actions / Azure DevOps
- Docker containerization
- Comprehensive API documentation (FastAPI auto-docs)
- Integration testing with sample webhooks

### Phase 6 (Optional - Stretch Goals)
- Simple admin UI using Streamlit (list analyses, view reports)
- Auto-generate fix PRs (create branch + commit suggested changes)
- Trend analysis (common failure patterns over time)
- Multi-repository support

---

## 6. Key Design Decisions

1. **LangGraph over simple chains:** Enables conditional routing, parallel agent execution, and state management across the multi-agent workflow.
2. **FAISS for vector search:** Lightweight, no external dependency for MVP. Can migrate to ChromaDB or Pinecone later.
3. **Streamlit for MVP frontend:** Rapid prototyping. Can replace with React for production.
4. **Modular agent design:** Each agent is independent and testable. Can swap LLM providers or add new agents without restructuring.
5. **Webhook-driven:** Allows real-time triggering from CI/CD systems rather than polling.

---

## 7. Learning Roadmap (For Beginners)

The skills below are ordered intentionally -- each level builds on the previous. Don't try to learn everything at once. Follow this sequence, do the mini-exercises, and by the end you'll have enough to start building.

---

### Level 1: Python Fundamentals (Pre-requisite)
**Why:** Everything in this project is Python. You need to be comfortable with it first.

**What to learn:**
- Variables, data types, loops, conditionals
- Functions and modules
- Classes and objects (basic OOP)
- File I/O (reading/writing files)
- `pip` and virtual environments (`python -m venv`)
- Dictionaries and JSON handling (`json` module)
- Exception handling (`try/except`)
- Type hints (e.g., `def greet(name: str) -> str:`)

**Practice exercise:**
Write a script that reads a `.log` text file, extracts all lines containing the word "ERROR", and writes them to a new file called `errors_only.log`.

---

### Level 2: Git Basics
**Why:** The Commit Analyzer Agent needs to work with git history. You also need git for your own version control.

**What to learn:**
- `git init`, `git add`, `git commit`, `git push`, `git pull`
- Branches: `git branch`, `git checkout`, `git merge`
- `git log` and `git diff` (reading commit history and changes)
- GitHub basics: repos, pull requests, issues
- `.gitignore` file

**Practice exercise:**
Create a repo, make 5 commits changing a Python file, then write a Python script using `subprocess` to run `git log --oneline` and parse the output into a list of dictionaries with `hash` and `message` keys.

---

### Level 3: APIs and HTTP Basics
**Why:** You'll be calling CI/CD APIs (GitHub Actions, Azure DevOps) and building your own API with FastAPI.

**What to learn:**
- What is an API, REST, HTTP methods (GET, POST, PUT, DELETE)
- Status codes (200, 201, 400, 404, 500)
- JSON request/response format
- Python `requests` library: `requests.get()`, `requests.post()`
- API authentication: API keys, Bearer tokens
- What is a webhook

**Practice exercise:**
Use the `requests` library to call the GitHub API (`https://api.github.com/repos/{owner}/{repo}/commits`) and print the last 5 commit messages from any public repo.

---

### Level 4: FastAPI
**Why:** This is the backend framework for our API layer.

**What to learn:**
- Install FastAPI and Uvicorn
- Create a basic app with `@app.get("/")` and `@app.post("/")`
- Path parameters and query parameters
- Request body with Pydantic models
- Returning JSON responses
- Running the server: `uvicorn main:app --reload`
- Testing endpoints with browser or `curl`

**Practice exercise:**
Build a FastAPI app with:
- `POST /logs` that accepts a JSON body `{"log_text": "...", "source": "github"}` and stores it in a Python list
- `GET /logs` that returns all stored logs
- `GET /logs/{id}` that returns a specific log by index

---

### Level 5: AWS Bedrock with Claude / LLM Basics
**Why:** The core intelligence of every agent comes from LLM calls.

**What to learn:**
- What are LLMs, tokens, prompts, completions
- AWS Bedrock: `boto3` library, model IDs, regions
- Claude API format: messages array, system prompt, anthropic_version
- `bedrock_runtime.invoke_model()` -- sending messages, getting responses
- System prompts vs user prompts (in messages)
- Temperature, max_tokens parameters
- Prompt engineering basics: be specific, provide context, use examples
- Structured output: asking Claude to respond in JSON

**AWS Setup (you'll do this):**
1. AWS Console → Bedrock → Model access → Enable Anthropic Claude models
2. AWS Console → IAM → Create user with `AmazonBedrockFullAccess` policy
3. Get Access Key ID and Secret Access Key
4. Configure AWS credentials (`~/.aws/credentials` or environment variables)
5. Choose region: `us-east-1` (recommended)
6. Model IDs:
   - `anthropic.claude-sonnet-4-5-v1:0` (faster, recommended)
   - `anthropic.claude-opus-4-6-v1:0` (most capable)

**Practice exercise:**
Write a script that takes a pipeline error log as a string, sends it to Claude via Bedrock with the system prompt "You are a CI/CD expert. Analyze this error log and identify the root cause.", and prints the response.

**Code example:**
```python
import boto3
import json

client = boto3.client(
    service_name='bedrock-runtime',
    region_name='us-east-1'
)

messages = [
    {"role": "user", "content": "Analyze this error: " + error_log}
]

body = json.dumps({
    "anthropic_version": "bedrock-2023-05-31",
    "max_tokens": 4096,
    "messages": messages
})

response = client.invoke_model(
    modelId="anthropic.claude-sonnet-4-5-v1:0",
    body=body
)

result = json.loads(response['body'].read())
print(result['content'][0]['text'])
```

---

### Level 6: LangChain Basics
**Why:** LangChain simplifies LLM integration, prompt management, and chaining operations.

**What to learn:**
- Install `langchain` and `langchain-aws`
- ChatModels: `ChatBedrock` from `langchain_aws` -- sending messages
- Prompt Templates: `ChatPromptTemplate` -- reusable prompts with variables
- Output Parsers: `StrOutputParser`, `JsonOutputParser`
- Chains: connecting prompt -> model -> parser with `|` (pipe operator)
- What are "Runnables" in LangChain (LCEL)

**Practice exercise:**
Create a LangChain chain that:
1. Takes `{error_log}` and `{recent_commits}` as input variables
2. Uses a prompt template asking for root cause analysis
3. Sends to ChatBedrock (Claude)
4. Parses the output as a JSON with keys: `root_cause`, `confidence`, `suggested_fix`

**Code example:**
```python
from langchain_aws import ChatBedrock
from langchain_core.prompts import ChatPromptTemplate
from langchain_core.output_parsers import JsonOutputParser

llm = ChatBedrock(
    model_id="anthropic.claude-sonnet-4-5-v1:0",
    region_name="us-east-1"
)

prompt = ChatPromptTemplate.from_messages([
    ("system", "You are a CI/CD expert. Respond in JSON."),
    ("user", "Error: {error_log}\nRecent commits: {recent_commits}")
])

chain = prompt | llm | JsonOutputParser()
result = chain.invoke({"error_log": "...", "recent_commits": "..."})
```

---

### Level 7: LangGraph (Multi-Agent Orchestration)
**Why:** This is how we coordinate multiple agents into a workflow.

**What to learn:**
- What is a state machine / graph-based workflow
- `StateGraph`: defining nodes and edges
- State: TypedDict for passing data between nodes
- Nodes: functions that process state
- Edges: conditional routing between nodes
- `graph.compile()` and `graph.invoke()`
- Human-in-the-loop patterns (optional)

**Practice exercise:**
Build a LangGraph with 3 nodes:
1. `parse_log` -- extracts error type from a log string
2. `analyze` -- sends error to LLM for root cause
3. `recommend` -- sends root cause to LLM for fix suggestion

Wire them: `parse_log -> analyze -> recommend`, run end-to-end.

---

### Level 8: Vector Stores and Embeddings (FAISS)
**Why:** For matching current failures against historical ones (similarity search).

**What to learn:**
- What are embeddings (text -> numerical vectors)
- AWS Bedrock embeddings: `amazon.titan-embed-text-v2:0` or use OpenAI embeddings API
- What is a vector store and similarity search
- FAISS: creating an index, adding vectors, querying
- LangChain's FAISS integration: `FAISS.from_texts()`, `similarity_search()`

**Options for embeddings:**
1. **AWS Titan Embeddings** (via Bedrock) - keeps everything in AWS
2. **OpenAI embeddings** - `text-embedding-3-small` (cheap, fast)
3. **Sentence Transformers** - free, local, but less powerful

**Practice exercise:**
Create a FAISS vector store with 10 sample error messages using Titan or OpenAI embeddings. Then query it with a new error message and retrieve the 3 most similar historical errors.

**Code example with Titan:**
```python
from langchain_aws import BedrockEmbeddings

embeddings = BedrockEmbeddings(
    model_id="amazon.titan-embed-text-v2:0",
    region_name="us-east-1"
)

from langchain_community.vectorstores import FAISS
vectorstore = FAISS.from_texts(error_messages, embeddings)
results = vectorstore.similarity_search("new error", k=3)
```

---

### Level 9: Docker Basics (Deployment)
**Why:** Packaging everything so it runs anywhere.

**What to learn:**
- What is Docker, images, containers
- `Dockerfile`: FROM, COPY, RUN, CMD
- Building an image: `docker build -t myapp .`
- Running a container: `docker run -p 8000:8000 myapp`
- `docker-compose.yml`: running multiple services together
- Environment variables in Docker

**Practice exercise:**
Dockerize your FastAPI app: write a Dockerfile, build it, and run it so you can access `http://localhost:8000/docs` from your browser.

---

### Suggested Learning Order Mapped to Project Phases

| Learning Level | Enables Project Phase |
|---|---|
| Level 1-2 (Python, Git) | Setup + basic scripting |
| Level 3-4 (APIs, FastAPI) | Phase 4: API Layer |
| Level 5-6 (Bedrock, LangChain) | Phase 1: Log Ingestion + RCA Agent |
| Level 7 (LangGraph) | Phase 4: Orchestrator |
| Level 8 (FAISS) | Phase 3: Historical matching |
| Level 9 (Docker) | Phase 5: Deployment |

**Recommended approach:** Complete Levels 1-6, then start building Phase 1 of the project. Learn Levels 7-10 as you reach the phases that need them. Building while learning is more effective than learning everything upfront.
