#!/bin/bash
# Health check script for Keepalived
# Returns 0 if service is healthy, 1 otherwise

CONTAINER_NAME="$1"
if [ -z "$CONTAINER_NAME" ]; then
    exit 1
fi

RUNNING=$(docker inspect --format='{{.State.Running}}' "$CONTAINER_NAME" 2>/dev/null)
if [ "$RUNNING" = "true" ]; then
    exit 0
else
    exit 1
fi