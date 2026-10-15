import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI

from ..infrastructure.database import create_schema
from ..infrastructure.seed import seed_demo_data
from .errors import register_error_handlers
from .routes import router

logging.basicConfig(level=logging.INFO, format="%(levelname)s %(name)s: %(message)s")


@asynccontextmanager
async def lifespan(_: FastAPI):
    create_schema()
    seed_demo_data()
    yield


app = FastAPI(title="Quest Campus — API Métier", version="1.0.0", lifespan=lifespan)
register_error_handlers(app)
app.include_router(router)


@app.get("/health")
def health() -> dict:
    return {"status": "ok"}
