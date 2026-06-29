import os

DATABASE_URL = os.environ.get("DATABASE_URL", "postgresql+asyncpg://htkis:htkis@127.0.0.1:5433/htkis_cloud")
SCREENSHOT_DIR = os.environ.get("SCREENSHOT_DIR", "/mnt/share/HCOffice")
JWT_SECRET = os.environ.get("JWT_SECRET", "change-me-in-production")
ADMIN_USERNAMES = os.environ.get("ADMIN_USERNAMES", "guacadmin").split(",")