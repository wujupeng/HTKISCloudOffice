import httpx
from config import GUACAMOLE_URL, GUACAMOLE_ADMIN_USER, GUACAMOLE_ADMIN_PASSWORD

_admin_token = None


async def get_admin_token() -> str:
    global _admin_token
    async with httpx.AsyncClient() as client:
        resp = await client.post(
            f"{GUACAMOLE_URL}/api/tokens",
            data={"username": GUACAMOLE_ADMIN_USER, "password": GUACAMOLE_ADMIN_PASSWORD},
            timeout=10.0,
        )
        if resp.status_code == 200:
            _admin_token = resp.json()["authToken"]
            return _admin_token
        raise Exception(f"Failed to get admin token: {resp.status_code}")


async def ensure_user_and_grant(username: str):
    token = await get_admin_token()
    headers = {"Guacamole-Token": token, "Content-Type": "application/json"}
    base = f"{GUACAMOLE_URL}/api/session/data/postgresql"

    async with httpx.AsyncClient() as client:
        resp = await client.get(f"{base}/users/{username}", headers=headers, timeout=10.0)
        if resp.status_code == 404:
            create_resp = await client.post(
                f"{base}/users",
                json={"username": username, "password": "", "attributes": {}},
                headers=headers,
                timeout=10.0,
            )
            if create_resp.status_code not in (200, 201):
                print(f"Failed to create user {username}: {create_resp.status_code} {create_resp.text}")
                return

        perms_resp = await client.get(
            f"{base}/users/{username}/permissions",
            headers=headers,
            timeout=10.0,
        )
        if perms_resp.status_code != 200:
            print(f"Failed to get permissions for {username}: {perms_resp.status_code}")
            return

        perms = perms_resp.json()
        if "1" not in perms.get("connectionPermissions", {}):
            perms.setdefault("connectionPermissions", {})["1"] = ["READ"]
            update_resp = await client.patch(
                f"{base}/users/{username}/permissions",
                json=perms,
                headers=headers,
                timeout=10.0,
            )
            if update_resp.status_code not in (200, 204):
                print(f"Failed to update permissions for {username}: {update_resp.status_code} {update_resp.text}")