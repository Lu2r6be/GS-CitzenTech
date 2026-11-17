#!/usr/bin/env bash
# mostrar-url-tunel.sh - mostra a URL publica atual do tunel (https://....trycloudflare.com).
# O endereco do "quick tunnel" muda a cada vez que o servico reinicia; ele aparece no log do servico.
# Esta URL e a que vai em API_BASE no firmware (Wokwi) e em EXPO_PUBLIC_API_URL no app.
set -uo pipefail
url="$(journalctl -u termometro-cloudflared --no-pager -n 300 2>/dev/null | grep -oE 'https://[a-z0-9-]+\.trycloudflare\.com' | tail -n1)"
if [[ -n "$url" ]]; then
  echo "$url"
else
  echo "Ainda nao apareceu uma URL. Confira: systemctl status termometro-cloudflared" >&2
  exit 1
fi
