import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI

from .errors import register_error_handlers
from .routes import router
from .seed import initialize

logging.basicConfig(level=logging.INFO, format="%(levelname)s %(name)s: %(message)s")


@asynccontextmanager
async def lifespan(_: FastAPI):
    initialize()
    yield


app = FastAPI(title="Quest Campus — API Auth", version="1.0.0", lifespan=lifespan)
register_error_handlers(app)
app.include_router(router)


@app.get("/health")
def health() -> dict:
    return {"status": "ok"}
