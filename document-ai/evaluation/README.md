The checked-in corpus contains eight synthetic English documents, with expected categories,
field values, important clause keywords and forbidden unsupported claims. It contains no user data.

Run from `document-ai`:

```sh
OLLAMA_MODEL=qwen3:4b PYTHONPATH=. .venv/bin/python -m evaluation.run --provider local --output evaluation/results.local.json
# Set GEMINI_API_KEY and GEMINI_EVALUATION_MODEL via your secret manager first.
PYTHONPATH=. .venv/bin/python -m evaluation.run --provider gemini --output evaluation/results.gemini.json
```

The evaluator reports classification accuracy, exact original-field recall, clause keyword recall,
quote grounding and wall-clock latency. Failed schema/grounding checks count as failed analyses.
Forbidden phrase counts are regression checks, not a measured semantic hallucination rate.
Summary quality, entity precision, OCR character error rates, action usefulness and human usefulness
need independently annotated data and reviewer ratings; they remain unmeasured.
The synthetic corpus and all passing tests are insufficient for broad production quality claims.

OCR benchmark: `PYTHONPATH=. .venv/bin/python -m evaluation.ocr`; real local HTTP smoke: `PYTHONPATH=. .venv/bin/python -m evaluation.smoke`.


Quality continuation: `quality.py` writes safe synthetic predictions and per-case metrics in
`predictions/` and `reports/`; `build_corpus.py` and `build_layouts.py` create the 22-case corpus.
Report hashes identify pipeline revisions. Diagnostic and broad-corpus reports are retained;
focused final regressions are separate. Model-judge support and target/keyword overlap are
proxies; independent human accuracy, usefulness and calibration are explicitly unmeasured.
See `review_rubric.md` and `docs/document-intelligence-quality.md` for review protocol and results.

OCR language assets are installed in Docker. Set `OCR_LANGUAGES=eng+msa+tam+chi_sim` for the
multilingual corpus. CER removes whitespace; Chinese WER needs a defined segmentation protocol.
`EXPLANATION_LANGUAGE` sets narrative language, while original source values remain untranslated.
Only generated corpus files are accepted by evaluation scripts; do not add private documents to
external Gemini reference evaluation. The service does not write source/prompt/model content to logs.
