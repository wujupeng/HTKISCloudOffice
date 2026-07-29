#!/usr/bin/env python3
"""Test replication connectivity from standby to master"""
import subprocess, sys

# Test 1: Check if master port is reachable from standby
result = subprocess.run(
    ["docker", "run", "--rm", "--network", "host", "-e", "PGPASSWORD=****",
     "postgres:15", "psql", "-h", "****", "-p", "5435",
     "-U", "replicator", "-d", "erp_guacamole_db", "-c", "SELECT 1;"],
    capture_output=True, text=True, timeout=30
)
print("STDOUT:", result.stdout)
print("STDERR:", result.stderr)
print("RC:", result.returncode)

if result.returncode != 0:
    print("FAILED: Cannot connect to master as replicator")
    sys.exit(1)
print("SUCCESS: Can connect to master as replicator")