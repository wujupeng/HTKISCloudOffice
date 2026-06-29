#!/bin/bash
find /mnt/share/HCOffice -type f -mtime +90 -delete 2>/dev/null
find /mnt/share/HCOffice -type d -empty -delete 2>/dev/null
echo "$(date '+%Y-%m-%d %H:%M:%S') screenshot cleanup completed" >> /var/log/htkis-screenshot-cleanup.log