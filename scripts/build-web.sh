#!/bin/bash
set -e

if [ -z "$NG_APP_AI_CONNECTION_STRING" ]; then
  echo "Error: NG_APP_AI_CONNECTION_STRING is not set"
  exit 1
fi

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROD_ENV="$ROOT/web/src/environments/environment.prod.ts"

restore() {
  cat > "$PROD_ENV" << 'EOF'
export const environment = {
  production: true,
  apiUrl: 'https://tom-nextech-api.ambitiousbush-5c2916fd.eastus.azurecontainerapps.io',
  appInsightsConnectionString: ''
};
EOF
}

trap restore EXIT

cat > "$PROD_ENV" << EOF
export const environment = {
  production: true,
  apiUrl: 'https://tom-nextech-api.ambitiousbush-5c2916fd.eastus.azurecontainerapps.io',
  appInsightsConnectionString: '$NG_APP_AI_CONNECTION_STRING'
};
EOF

cd "$ROOT/web" && npx ng build --configuration=production
