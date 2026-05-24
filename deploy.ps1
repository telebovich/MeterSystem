#Requires -Version 5.1
$ErrorActionPreference = "Stop"

function Invoke-Command-Checked {
    param([scriptblock]$Command)
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE"
    }
}

Write-Host "Downloading Images..."
Invoke-Command-Checked { minikube image pull postgres:18 }
Invoke-Command-Checked { minikube image pull rabbitmq:3-management }

Write-Host "Deploying Queue..."
Invoke-Command-Checked { kubectl apply -f queue/deploy.yaml }

Write-Host "Deploying Database..."
Invoke-Command-Checked { kubectl apply -f database/deploy.yaml }
Invoke-Command-Checked { kubectl wait --for=condition=ready pod -l app=postgres --timeout=300s }

$podName = kubectl get pod -l app=postgres -o jsonpath='{.items[0].metadata.name}'
if ($LASTEXITCODE -ne 0) { throw "Failed to get postgres pod name" }

$schemaSql = Get-Content -Raw "database/schema.sql"
$schemaSql | Invoke-Command-Checked {
    kubectl exec -i $podName -- sh -c 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
}

Write-Host "Publishing and Deploying API..."
Invoke-Command-Checked { dotnet publish ./src/MeterSystem.Api/MeterSystem.Api.csproj -t:PublishContainer }
Invoke-Command-Checked { minikube image load metersystem-api:latest }
Invoke-Command-Checked { kubectl apply -f ./src/MeterSystem.Api/deploy.yaml }

Write-Host "Deploying Worker..."
Invoke-Command-Checked { dotnet publish ./src/MeterSystem.Worker/MeterSystem.Worker.csproj -t:PublishContainer }
Invoke-Command-Checked { minikube image load metersystem-worker:latest }
Invoke-Command-Checked { kubectl apply -f ./src/MeterSystem.Worker/deploy.yaml }

Write-Host "Done"