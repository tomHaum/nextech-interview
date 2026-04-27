@description('Location for all resources except Static Web App')
param location string = 'eastus'

@description('Location for Static Web App (limited region support)')
param swaLocation string = 'eastus2'

@description('Base name used to derive resource names')
param appName string = 'nextech'

@description('App Service name (appears in public URL)')
param apiAppName string = 'tom-nextech-api'

// Log Analytics workspace — required by modern Application Insights
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

resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: '${appName}-plan'
  location: location
  sku: { name: 'B1', tier: 'Basic' }
  kind: 'linux'
  properties: { reserved: true }
}

resource staticWebApp 'Microsoft.Web/staticSites@2022-09-01' = {
  name: '${appName}-web'
  location: swaLocation
  sku: { name: 'Free', tier: 'Free' }
  properties: {}
}

resource appService 'Microsoft.Web/sites@2022-09-01' = {
  name: apiAppName
  location: location
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
          value: '~3'
        }
        {
          name: 'HackerNews__BaseUrl'
          value: 'https://hacker-news.firebaseio.com/v0/'
        }
        {
          name: 'HackerNews__MaxStories'
          value: '500'
        }
        {
          name: 'HackerNews__RefreshIntervalSeconds'
          value: '60'
        }
        {
          name: 'HackerNews__ItemFetchConcurrency'
          value: '10'
        }
        {
          name: 'Cors__AllowedOrigins__0'
          value: 'https://${staticWebApp.properties.defaultHostname}'
        }
      ]
    }
  }
}

output apiUrl string = 'https://${appService.properties.defaultHostName}'
output webUrl string = 'https://${staticWebApp.properties.defaultHostname}'
output appInsightsName string = appInsights.name
