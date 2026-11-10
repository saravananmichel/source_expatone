# Private CPU-only Ollama DEV image. Build context: document-ai/ollama-dev
FROM ollama/ollama:0.12.11
ENV OLLAMA_HOST=0.0.0.0:8080 \
    OLLAMA_MODELS=/opt/ollama/models \
    OLLAMA_NUM_PARALLEL=1 \
    OLLAMA_MAX_LOADED_MODELS=1
RUN mkdir -p /opt/ollama/models && \
    (ollama serve > /tmp/ollama-build.log 2>&1 & pid=$!; \
     trap 'kill $pid 2>/dev/null || true' EXIT; \
     i=0; until ollama list >/dev/null 2>&1; do \
       i=$((i+1)); if [ "$i" -ge 90 ]; then cat /tmp/ollama-build.log; exit 1; fi; sleep 2; \
     done; \
     ollama pull qwen3:0.6b && \
     ollama list | grep -F 'qwen3:0.6b')
EXPOSE 8080
# Cloud Run IAM must require authenticated invocations. Do not enable unauthenticated access.
ENTRYPOINT ["/bin/ollama"]
CMD ["serve"]
