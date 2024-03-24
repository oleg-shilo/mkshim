pipeline {
  agent any
  stages {
    stage('Clean workspace') {
      steps {
        sh 'cleamWs()'
      }
    }

    stage('Git Checkout') {
      steps {
        sh 'git branch: \'<your-brach>\', credentialsId: \'<id-of-Jenkins-credentials>\', url: \'<url to your GitHub repository\''
      }
    }

    stage('Restore packages') {
      steps {
        sh 'bat "dotnet restore ${workspace}\\\\src\\\\mkshim.sln"'
      }
    }

    stage('Clean') {
      steps {
        sh '''stage(\'Clean\') {
  steps {
    bat "msbuild.exe ${workspace}\\\\src\\\\mkshim.sln" /nologo /nr:false /p:platform=\\"x64\\" /p:configuration=\\"release\\" /t:clean"
  }
}'''
        }
      }

      stage('Increase version') {
        steps {
          powershell 'ddsds'
          sh '''echo "${env.BUILD_NUMBER}"
        powershell \'\'\'
           $xmlFileName = "<path-to-solution>\\\\<package-project-name>\\\\Package.appxmanifest"     
           [xml]$xmlDoc = Get-Content $xmlFileName
           $version = $xmlDoc.Package.Identity.Version
           $trimmedVersion = $version -replace \'.[0-9]+$\', \'.\'
           $xmlDoc.Package.Identity.Version = $trimmedVersion + ${env:BUILD_NUMBER}
           echo \'New version:\' $xmlDoc.Package.Identity.Version
           $xmlDoc.Save($xmlFileName)
        \'\'\''''
        }
      }

      stage('Build') {
        steps {
          sh 'bat "msbuild.exe ${workspace}\\\\<path-to-solution>\\\\<solution-name>.sln /nologo /nr:false  /p:platform=\\"x64\\" /p:configuration=\\"release\\"  /t:clean;restore;rebuild"'
        }
      }

    }
  }