## GitCloud-api
A Service that allow you to use your GitHub repository as a Cloud

## Features
* JWT refesh access token scheme with EdDsa encryption
* File tree caching
* File modify Queue (to prevent race condition on github rest api endpoint)
* Usage of GitHub rest api and GraphQl to minimise request time
* All tokens and users data are kept in repository whereby deletion of api would not destroy the data

## Installation
* Download source files / clone reposiotory
* Build
* Moddify/fill out appsettings.json
* run
