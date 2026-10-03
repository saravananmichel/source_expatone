# Independent review and calibration protocol

Use blinded predictions, randomize model labels, and keep synthetic gold separate from prompts.
Two independent reviewers score each extracted field, relationship, finding, action, overview,
and explanation: 0 incorrect, 1 partially correct, 2 correct, 3 useful and contextual.
Review evidence entailment separately from quotation matching. Confirm modality, negation,
actor, conditions, date and money units, scope, and both sides of contradictions.
Record unsupported additional assertions even when a copied quotation exists.

Gold must record source page and exact quotation for every positive assertion. Absence targets
must name the analyzed scope. Do not mark a statement absent from the entire agreement just
because it was absent from one excerpt. Annotate missing schedules separately.

For confidence calibration, label each significant claim correct/incorrect after adjudication.
Split documents (not claims) into development, calibration and held-out test sets. Compare
model confidence bins with adjudicated correctness, report coverage, ECE/Brier score and sample
counts; include uncertain and rejected claims. Choose thresholds on calibration only. Report
qualitative uncertainty until held-out calibration is available. Current confidence is NOT calibrated.

Current checked-in gold is partial, author-defined synthetic targets. Empty lists mean unannotated,
not verified absent. Keyword overlap is a retrieval proxy; model-judge support is a screening
result. Neither is independent semantic accuracy. Human review scores: Not measured.
