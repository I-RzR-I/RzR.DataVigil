@echo off
cls

echo Write the source branch name (you current branch name, where were the changes made): 
set /p userDefinedSourceBranch=
echo.
echo Write the destination branch name (the branch from which you can get the latest changes, like 'develop' or 'main/master'): 
set /p userDefinedDestinationBranch=
echo.
echo Write your custom version number: 
set /p userDefinedVersion=
echo.

:: Set init params value
set applicationName=RzR.DataVigil
set runVersionIncrement=y
set runGenChangeLog=y
:: If runBuild > y(yes), build in release mode
set runBuild=y 
set runSolutionTest=n
set runTest=y
set runPack=y
set assemblyPath=$('..\src\shared\GeneralAssemblyInfo.cs')
set genType=-2
set setInChangeLogNewVersion=y
set autoCommitAndPush=n
set autoGetLatestDevelop=y
set changeLogPath=$('..\docs\CHANGELOG.MD')
set sourceBranch=%userDefinedSourceBranch%
set destinationBranch=%userDefinedDestinationBranch%
set customVersion=%userDefinedVersion%
set solutionPath=$('..\src\RzR.DataVigil.sln')
set packResultPath=$('..\nuget\')
set packProjectsPath=$('..\src\code\RzR.DataVigil.Abstractions\RzR.DataVigil.Abstractions.csproj','..\src\code\RzR.DataVigil.Core\RzR.DataVigil.Core.csproj','..\src\code\RzR.DataVigil.AspNetCore\RzR.DataVigil.AspNetCore.csproj','..\src\code\provider\RzR.DataVigil.EFCore\RzR.DataVigil.EFCore.csproj','..\src\code\storage\RzR.DataVigil.Storage.File\RzR.DataVigil.Storage.File.csproj','..\src\code\storage\ef\RzR.DataVigil.Storage.EfSqlServer\RzR.DataVigil.Storage.EfSqlServer.csproj','..\src\code\storage\ef\RzR.DataVigil.Storage.EfPostgreSql\RzR.DataVigil.Storage.EfPostgreSql.csproj','..\src\code\storage\ef\RzR.DataVigil.Storage.EfMongoDb\RzR.DataVigil.Storage.EfMongoDb.csproj')
set testProjectsPath=$('..\src\tests\RzR.DataVigil.Core.Tests\RzR.DataVigil.Core.Tests.csproj','..\src\tests\RzR.DataVigil.AspNetCore.Tests\RzR.DataVigil.AspNetCore.Tests.csproj','..\src\tests\RzR.DataVigil.EFCore.Tests\RzR.DataVigil.EFCore.Tests.csproj','..\src\tests\RzR.DataVigil.Storage.EfSqlServer.Tests\RzR.DataVigil.Storage.EfSqlServer.Tests.csproj','..\src\tests\RzR.DataVigil.Storage.File.Tests\RzR.DataVigil.Storage.File.Tests.csproj')


echo :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
echo :::         Initialize:                                           :::
echo :::            - New application version generation               :::
echo :::            - Change log generation                            :::
echo :::            - Build                                            :::
echo :::            - Test                                             :::
echo :::            - Create package                                   :::
echo :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
echo:
echo:

PowerShell -NoProfile -ExecutionPolicy ByPass -Command ".\GenerateBuildInfo.exe -scriptCommands \"runVersionIncrement=%runVersionIncrement%;runGenChangeLog=%runGenChangeLog%;runBuild=%runBuild%;runSolutionTest=%runSolutionTest%;runTest=%runTest%;runPack=%runPack%;setInChangeLogNewVersion=%setInChangeLogNewVersion%;autoCommitAndPush=%autoCommitAndPush%;autoGetLatestDevelop=%autoGetLatestDevelop%;changeLogPath=%changeLogPath%;sourceBranch=%sourceBranch%;destinationBranch=%destinationBranch%;assemblyPath=%assemblyPath%;customVersion=%customVersion%;genType=%genType%;solutionPath=%solutionPath%;packResultPath=%packResultPath%;packProjectsPath=%packProjectsPath%;testProjectsPath=%testProjectsPath%\"";

echo
pause
