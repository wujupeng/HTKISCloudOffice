import os

DATABASE_URL = os.environ.get("DATABASE_URL", "postgresql+asyncpg://htkis:htkis@127.0.0.1:5433/htkis_cloud")
GUACAMOLE_URL = os.environ.get("GUACAMOLE_URL", "http://127.0.0.1:8081/guacamole")
SMTP_HOST = os.environ.get("SMTP_HOST", "")
SMTP_PORT = int(os.environ.get("SMTP_PORT", "465"))
SMTP_USER = os.environ.get("SMTP_USER", "")
SMTP_PASSWORD = os.environ.get("SMTP_PASSWORD", "")
SMTP_FROM = os.environ.get("SMTP_FROM", "HTKIS Cloud Office <noreply@example.com>")
SMTP_USE_TLS = os.environ.get("SMTP_USE_TLS", "true").lower() == "true"
JWT_SECRET = os.environ.get("JWT_SECRET", "change-me-in-production")
JWT_EXPIRE_DAYS = int(os.environ.get("JWT_EXPIRE_DAYS", "30"))
ADMIN_USERNAMES = os.environ.get("ADMIN_USERNAMES", "guacadmin").split(",")