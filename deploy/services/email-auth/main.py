from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from routers import auth, login, admin
from database import engine, Base

app = FastAPI(title="HTKIS Email Auth Service", docs_url="/docs")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(auth.router, prefix="/api/email-auth", tags=["auth"])
app.include_router(login.router, prefix="/api/email-auth", tags=["login"])
app.include_router(admin.router, prefix="/api/email-auth/admin", tags=["admin"])
app.include_router(auth.router, tags=["auth-root"])


@app.on_event("startup")
async def startup():
    async with engine.begin() as conn:
        await conn.run_sync(Base.metadata.create_all)