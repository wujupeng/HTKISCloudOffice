import time
from fastapi import FastAPI, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse
from starlette.middleware.base import BaseHTTPMiddleware
from routers import auth, login, admin
from database import engine, Base

app = FastAPI(title="ERP Email Auth Service", docs_url=None, redoc_url=None)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


class RateLimitMiddleware(BaseHTTPMiddleware):
    def __init__(self, app, max_requests: int = 10, window_seconds: int = 60):
        super().__init__(app)
        self.max_requests = max_requests
        self.window_seconds = window_seconds
        self._requests = {}

    async def dispatch(self, request: Request, call_next):
        if request.url.path == "/api/erp-auth/login" and request.method == "POST":
            client_ip = request.client.host if request.client else "unknown"
            key = f"login:{client_ip}"
            now = time.time()
            if key in self._requests:
                timestamps = [t for t in self._requests[key] if now - t < self.window_seconds]
                if len(timestamps) >= self.max_requests:
                    return JSONResponse(
                        status_code=429,
                        content={"success": False, "error_code": "RATE_LIMITED", "message": "请求过于频繁，请稍后重试"}
                    )
                timestamps.append(now)
                self._requests[key] = timestamps
            else:
                self._requests[key] = [now]

            if len(self._requests) > 10000:
                cutoff = now - self.window_seconds
                self._requests = {k: v for k, v in self._requests.items() if v and v[-1] > cutoff}

        return await call_next(request)


app.add_middleware(RateLimitMiddleware, max_requests=10, window_seconds=60)

app.include_router(auth.router, prefix="/api/erp-auth", tags=["auth"])
app.include_router(login.router, prefix="/api/erp-auth", tags=["login"])
app.include_router(admin.router, prefix="/api/erp-auth/admin", tags=["admin"])
app.include_router(auth.router, tags=["auth-root"])


@app.exception_handler(Exception)
async def global_exception_handler(request: Request, exc: Exception):
    return JSONResponse(
        status_code=500,
        content={"success": False, "error_code": "INTERNAL_ERROR", "message": "服务器内部错误"}
    )


@app.on_event("startup")
async def startup():
    async with engine.begin() as conn:
        await conn.run_sync(Base.metadata.create_all)
