pipeline {
  agent any
  stages {
    stage('Clean workspace') {
      steps {
        cleamWs()
      }
    }

    stage('Git Checkout') {
      steps {
        git branch: master, credentialsId: 'mkshim-access-token', url: 'https://github.com/oleg-shilo/mkshim'
      }
    }

    stage('Restore packages') {
      steps {
        bat 'dotnet restore ${workspace}\\src\\mkshim.sln'
      }
    }

    stage('Clean') {
      steps {
        bat 'msbuild.exe "${workspace}\\src\\mkshim.sln" /nologo /nr:false /p:platform="x64" /p:configuration="release" /t:clean'
        }
      }

    stage('Build') {
      steps {
        bat 'msbuild.exe ${workspace}\\src\\mkshim.sln /nologo /nr:false  /p:platform="x64" /p:configuration="release" /t:clean;restore;rebuild'
      }
    }
  }
}