# Serviço de IA (ADR-0001) em contêiner, para o `docker compose` da raiz (RNF15).
# Mesmo Python dos testes e as mesmas versões do requirements.txt. Roda só em CPU.
FROM python:3.13.16-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1 \
    PIP_DISABLE_PIP_VERSION_CHECK=1 \
    TF_CPP_MIN_LOG_LEVEL=2

WORKDIR /app

# Dependências antes do código: mudar o código não reinstala o TensorFlow.
COPY requirements.txt .
RUN pip install -r requirements.txt

COPY pyproject.toml .
COPY dermatoglifia_ia ./dermatoglifia_ia
COPY servico ./servico
RUN pip install --no-deps . && rm -rf build *.egg-info

# Usuário sem privilégios. O serviço não grava nada em disco; o compose monta a
# raiz como somente leitura e dá um /tmp em memória (HOME aponta para lá).
RUN useradd --system --uid 10001 --no-create-home --shell /usr/sbin/nologin ia
USER ia

# Os modelos não entram na imagem: o compose monta as pastas (ADR-0005).
ENV HOME=/tmp \
    DERMATOGLIFIA_HOST=0.0.0.0 \
    DERMATOGLIFIA_PORTA=8000 \
    DERMATOGLIFIA_MODELO_DEMO=/modelos/demo \
    DERMATOGLIFIA_MODELO_PRODUCAO=/modelos/producao

EXPOSE 8000

# /saude responde 503 quando não há modelo válido: o contêiner fica "unhealthy".
HEALTHCHECK --interval=15s --timeout=5s --start-period=60s --retries=3 \
    CMD ["python", "-c", "import urllib.request; urllib.request.urlopen('http://127.0.0.1:8000/saude', timeout=4)"]

CMD ["python", "-m", "servico"]
