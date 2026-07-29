import os

DATABASE_URL = os.environ.get("DATABASE_URL", "postgresql+asyncpg://erp_guacamole:erp_guacamole@127.0.0.1:5435/erp_guacamole_db")
GUACAMOLE_URL = os.environ.get("GUACAMOLE_URL", "http://127.0.0.1:8082/guacamole")
SMTP_HOST = os.environ.get("SMTP_HOST", "")
SMTP_PORT = int(os.environ.get("SMTP_PORT", "465"))
SMTP_USER = os.environ.get("SMTP_USER", "")
SMTP_PASSWORD = os.environ.get("SMTP_PASSWORD", "")
SMTP_FROM = os.environ.get("SMTP_FROM", "ERP Cloud Office <noreply@example.com>")
SMTP_USE_TLS = os.environ.get("SMTP_USE_TLS", "true").lower() == "true"
JWT_SECRET = os.environ.get("JWT_SECRET", "change-me-in-production")
JWT_EXPIRE_DAYS = int(os.environ.get("JWT_EXPIRE_DAYS", "30"))
ADMIN_USERNAMES = os.environ.get("ADMIN_USERNAMES", "guacadmin").split(",")
GUACAMOLE_ADMIN_USER = os.environ.get("GUACAMOLE_ADMIN_USER", "guacadmin")
GUACAMOLE_ADMIN_PASSWORD = os.environ.get("GUACAMOLE_ADMIN_PASSWORD", "guacadmin")