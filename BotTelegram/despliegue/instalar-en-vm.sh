#!/usr/bin/env bash
# =====================================================================
#  SGAPE - Instala o actualiza el bot en la VM (Ubuntu). Lo ejecuta
#  subir-bot-a-vm.bat por SSH; no hace falta correrlo a mano.
#  Espera en /tmp/sgape-bot-paquete/: sgape-bot, wallet/, sgape-bot.env, sgape-bot.service
# =====================================================================
set -euo pipefail
P=/tmp/sgape-bot-paquete

sudo apt-get update -qq
sudo apt-get install -y -qq fonts-dejavu-core libfontconfig1 libicu-dev >/dev/null   # fuentes para PDF + idioma

id sgape >/dev/null 2>&1 || sudo useradd --system --home /opt/sgape-bot --shell /usr/sbin/nologin sgape
sudo mkdir -p /opt/sgape-bot
sudo systemctl stop sgape-bot 2>/dev/null || true

sudo install -m 755 "$P/sgape-bot" /opt/sgape-bot/sgape-bot
sudo rm -rf /opt/sgape-bot/wallet && sudo cp -r "$P/wallet" /opt/sgape-bot/wallet
sudo chown -R sgape:sgape /opt/sgape-bot && sudo chmod 700 /opt/sgape-bot/wallet

# El .env lleva los secretos: solo root lo lee (systemd se lo pasa al bot)
sudo install -m 600 -o root -g root "$P/sgape-bot.env" /etc/sgape-bot.env
sudo install -m 644 "$P/sgape-bot.service" /etc/systemd/system/sgape-bot.service

sudo systemctl daemon-reload
sudo systemctl enable --now sgape-bot
rm -rf "$P"

sleep 8
sudo systemctl --no-pager status sgape-bot | head -n 5
echo "---- ultimas lineas del bot ----"
sudo journalctl -u sgape-bot -n 8 --no-pager
