@description('Location for all resources except Static Web App')
param location string = 'eastus'

@description('Location for Static Web App (limited region support)')
param swaLocation string = 'eastus2'

@description('Base name used to derive resource names')
param appName string = 'nextech'

@description('Container App name (appears in public URL)')
param apiAppName string = 'tom-nextech-api'

// Log Analytics workspace — required by App Insights and Container Apps
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: '${appName}-logs'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${appName}-insights'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

resource staticWebApp 'Microsoft.Web/staticSites@2022-09-01' = {
  name: '${appName}-web'
  location: swaLocation
  sku: { name: 'Free', tier: 'Free' }
  properties: {}
}

// ACR name must be alphanumeric only, 5–50 chars
resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: '${appName}registry'
  location: location
  sku: { name: 'Basic' }
  properties: { adminUserEnabled: true }
}

resource containerAppsEnv 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: '${appName}-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: apiAppName
  location: location
  properties: {
    managedEnvironmentId: containerAppsEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
      }
      registries: [
        {
          server: containerRegistry.properties.loginServer
          username: containerRegistry.listCredentials().username
          passwordSecretRef: 'registry-password'
        }
      ]
      secrets: [
        {
          name: 'registry-password'
          value: containerRegistry.listCredentials().passwords[0].value
        }
        {
          name: 'appinsights-connection-string'
          value: appInsights.properties.ConnectionString
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          // Placeholder image replaced after first `az containerapp update`
          image: 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
          env: [
            { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', secretRef: 'appinsights-connection-string' }
            { name: 'HackerNews__BaseUrl', value: 'https://hacker-news.firebaseio.com/v0/' }
            { name: 'HackerNews__MaxStories', value: '500' }
            { name: 'HackerNews__RefreshIntervalSeconds', value: '60' }
            { name: 'HackerNews__ItemFetchConcurrency', value: '10' }
            { name: 'Cors__AllowedOrigins__0', value: 'https://${staticWebApp.properties.defaultHostname}' }
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
          ]
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 1
      }
    }
  }
}

output apiUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
output webUrl string = 'https://${staticWebApp.properties.defaultHostname}'
output acrLoginServer string = containerRegistry.properties.loginServer
output acrName string = containerRegistry.name
