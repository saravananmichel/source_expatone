# Human Evaluation Rubric — Document Intelligence Engine

## Purpose

Blinded human evaluation of document analysis outputs.
Reviewers score each output without knowing which system produced it.
Systems are labeled A, B, C in randomized per-document order.

## Scoring dimensions (1–5 scale)

| Score | Meaning |
|---|---|
| 1 | Incorrect / harmful / completely unhelpful |
| 2 | Partial / mostly wrong or misleading |
| 3 | Acceptable but incomplete or unclear |
| 4 | Good — accurate, useful, mostly complete |
| 5 | Excellent — accurate, complete, clear and actionable |

### 1. Factual accuracy
Does the output correctly represent what is stated in the document?
Penalize: invented facts, wrong amounts, wrong dates, wrong names, inverted negations.

### 2. Completeness
Does the output identify the key fields, clauses, dates, parties and obligations?
Penalize: missing important fields (expiry date, salary, parties), omitted key clauses.

### 3. Explanation quality
Does the output explain what the document means in plain language?
Penalize: bare field dumps (just "Salary: RM12,000"), jargon without explanation, missing context.

### 4. Actionability
Does the output tell the reader what they may need to do?
Penalize: no suggested actions when actions are clearly indicated, vague advice, invented obligations.

### 5. Grounding / traceability
Are claims traceable to specific document text?
Penalize: claims without evidence, evidence that doesn't support the claim, vague references.

### 6. Hallucination absence
Does the output avoid adding information not in the document?
Penalize: invented legal requirements, inferred rights/obligations, unsupported conclusions.
Score 5 = zero detected hallucinations.

### 7. Readability
Is the output written in clear, natural prose that a non-expert can understand?
Penalize: awkward phrasing, run-on sentences, overly technical language, mixed languages unexpectedly.

### 8. Overall usefulness
Would this output help the reader understand and act on their document?
This is a holistic score — consider accuracy, completeness and clarity together.

## Evaluation protocol

1. Prepare 10–20 representative documents covering: employment contract, passport/pass, insurance, tenancy, bank statement, government letter, offer letter, multilingual documents.
2. For each document, generate outputs from each system (Local/qwen3:4b, Local/qwen3:8b, Gemini).
3. Randomize system labels A/B/C per document.
4. Reviewer reads the ORIGINAL document, then scores each output independently.
5. Do not tell reviewers which system produced which output until after scoring.
6. Minimum 2 reviewers per document; compute inter-rater agreement (Cohen's kappa).

## Reporting

- Report mean score per dimension per system.
- Report per-document winner.
- Report hallucination count per system.
- Do NOT claim statistical significance from fewer than 30 documents.
- State clearly: "This is an engineering evaluation, not a scientific publication."
- Retain anonymized raw scores in `evaluation/reports/human_eval_*.json`.

## Limitations to state explicitly

- Small sample (10–20 documents) cannot establish corpus-wide quality.
- Reviewers may have document-type expertise bias.
- Synthetic documents in the corpus do not represent real-world scan quality.
- Model-judge support verdicts are separate from human entailment judgments.
- Inter-rater agreement must be reported; do not average without checking kappa ≥ 0.6.
