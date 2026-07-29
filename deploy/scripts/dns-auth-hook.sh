#!/bin/bash
echo ""
echo "============================================"
echo "Please add the following DNS TXT record:"
echo "  Name:  _acme-challenge.erp.oascii.com"
echo "  Value: $CERTBOT_VALIDATION"
echo ""
echo "After adding, press Enter to continue..."
echo "============================================"
read -r dummy
echo "Waiting 30 seconds for DNS propagation..."
sleep 30