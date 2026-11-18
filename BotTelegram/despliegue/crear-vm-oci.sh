#!/usr/bin/env bash
# =====================================================================
#  SGAPE - Crea la VM GRATUITA del bot (Ampere A1, Ubuntu 24.04) desde
#  Oracle Cloud Shell y reintenta cada minuto hasta que Oracle tenga cupo.
#  Uso (en Cloud Shell):  bash crear-vm-oci.sh [intentos]   (por defecto 180 = 3 horas)
#  Solo lleva la llave PUBLICA SSH (no es secreta). La privada queda en el PC.
# =====================================================================
set -u
PUB='ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABAQDJw0FOpYmk7BhETsMrGs9G2xefszbPrabOkH8lhX+9XtnHfueGpvxbtS5iCqczROprB8xxjs+QVMa5SNuLnMy/RYm6MVG6cKjlAe1YgSng46w2kg50x7Q5sYeyUH2xSWKuQ/JhYluyJFBPlkUmvyJtNH6Se0ycWvypbd0cxlgnKvktT5ho05J8GqKzrtLVa+8zBhfblYijf1f3bQ+qNIr/EaRflbklX4PfhIFf1vdWnNHvlcaP5lP8s3QWlos8GW4vy8FjStBMAS+ECKGAuKq2bhnAq4cTwFs39DwZnqhLCmwQAaeLR4gk0LFK44M2UIhAx+h64EuOETLSbQRJfsWB'
T=${OCI_TENANCY:?Ejecuta esto en Oracle Cloud Shell}

ip_de() { oci compute instance list-vnics --instance-id "$1" --query 'data[0]."public-ip"' --raw-output; }

# Si ya existe, solo muestra la IP
YA=$(oci compute instance list -c "$T" --display-name sgape-bot --lifecycle-state RUNNING --query 'data[0].id' --raw-output 2>/dev/null)
if [ -n "$YA" ] && [ "$YA" != "None" ]; then echo "LISTO: la VM ya existe. IP publica: $(ip_de "$YA")"; exit 0; fi

AD=$(oci iam availability-domain list -c "$T" --query 'data[0].name' --raw-output)
SUB=$(oci network subnet list -c "$T" --all --query "data[?contains(\"display-name\",'public')].id | [0]" --raw-output)
IMG=$(oci compute image list -c "$T" --operating-system 'Canonical Ubuntu' --operating-system-version '24.04' \
      --shape VM.Standard.A1.Flex --sort-by TIMECREATED --sort-order DESC --all --query 'data[0].id' --raw-output)
echo "$PUB" > ~/sgape.pub
echo "Zona: $AD | Subred: ...${SUB: -8} | Imagen: ...${IMG: -8}"
[ -z "$SUB" ] || [ "$SUB" = "None" ] && { echo "No encontre la subred publica (sgape-red)."; exit 1; }

for i in $(seq 1 "${1:-180}"); do
  if oci compute instance launch -c "$T" --availability-domain "$AD" --shape VM.Standard.A1.Flex \
       --shape-config '{"ocpus":1,"memoryInGBs":6}' --image-id "$IMG" --subnet-id "$SUB" \
       --assign-public-ip true --display-name sgape-bot --ssh-authorized-keys-file ~/sgape.pub \
       --wait-for-state RUNNING --query 'data.id' --raw-output > ~/vm_id.txt 2> ~/vm_err.txt; then
    echo "LISTO: VM creada. IP publica: $(ip_de "$(tail -n1 ~/vm_id.txt)")"
    exit 0
  fi
  MSG=$(grep -o 'Out of capacity[^.]*' ~/vm_err.txt | head -1)
  echo "$(date +%H:%M) intento $i: ${MSG:-$(tr -d '\n' < ~/vm_err.txt | head -c 200)}"
  sleep 60
done
echo "Sin cupo despues de $i intentos. Vuelve a ejecutarlo mas tarde."
