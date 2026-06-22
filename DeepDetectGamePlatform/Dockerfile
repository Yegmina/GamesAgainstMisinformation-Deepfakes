FROM python:3.12-slim

WORKDIR /app

ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1
ENV AI_PROVIDER=openai

COPY requirements.txt ./
RUN pip install --no-cache-dir -r requirements.txt

COPY backend ./backend

RUN mkdir -p /app/data

EXPOSE 8765

CMD ["uvicorn", "backend.app.main:app", "--host", "0.0.0.0", "--port", "8765"]
