::
::
echo off
echo........................................
echo Generate solution code from added Entity classes
echo........................................
echo off
:PROMPT
SET /P AREYOUSURE=Are you sure you want to delete generated files(Y/[N])?
IF /I "%AREYOUSURE%" NEQ "Y" GOTO END

::Select the VS version
::SET tt="C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\Common7\IDE\TextTransform.exe"
::SET tt="C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\Common7\IDE\TextTransform.exe"
::SET tt="C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Common7\IDE\TextTransform.exe"
::SET tt="C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\Common7\IDE\TextTransform.exe"
::SET tt="C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\Common7\IDE\TextTransform.exe"
SET tt="C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\Common7\IDE\TextTransform.exe"

echo off
echo Delete previously generated cs code files 
 DEL /F "1_t4EntityHelpersGenerate.cs"
 DEL /F "..\GWSuncoastAPI.Domain\Domain\2_t4DomainViewModelsGenerate.cs"	
 DEL /F "..\GWSuncoastAPI.Domain\Mapping\3_t4DomainMappingProfileGenerate.cs"	
 DEL /F "..\GWSuncoastAPI.Domain\Service\4_t4DomainServicesGenerate.cs"	
 DEL /F "..\GWSuncoastAPI.Api\Controllers\5_t4ApiControllerGenerate.cs"	
 DEL /F "..\GWSuncoastAPI.Api\5_t4ApiStartupAdditionsGenerate.cs"	
 DEL /F "..\GWSuncoastAPI.Test\6_t4IntegrationTestGenerate.cs"
echo .
echo Run all T4s...
echo -generate entity helpers
%tt% "1_t4EntityHelpersGenerate.tt" -out "1_t4EntityHelpersGenerate.cs"
echo -generate domain classes
%tt% "..\GWSuncoastAPI.Domain\Domain\2_t4DomainViewModelsGenerate.tt" -out "..\GWSuncoastAPI.Domain\Domain\2_t4DomainViewModelsGenerate.cs"
echo -generate mapper classes
%tt% "..\GWSuncoastAPI.Domain\Mapping\3_t4DomainMappingProfileGenerate.tt" -out "..\GWSuncoastAPI.Domain\Mapping\3_t4DomainMappingProfileGenerate.cs"	
echo -generate services classes
%tt% "..\GWSuncoastAPI.Domain\Service\4_t4DomainServicesGenerate.tt" -out "..\GWSuncoastAPI.Domain\Service\4_t4DomainServicesGenerate.cs"	
echo -generate controller classes
%tt% "..\GWSuncoastAPI.Api\Controllers\5_t4ApiControllerGenerate.tt" -out "..\GWSuncoastAPI.Api\Controllers\5_t4ApiControllerGenerate.cs"
echo -generate extended Startup code
%tt% "..\GWSuncoastAPI.Api\5_t4ApiStartupAdditionsGenerate.tt" -out "..\GWSuncoastAPI.Api\5_t4ApiStartupAdditionsGenerate.cs"	
echo -generate test classes
%tt% "..\GWSuncoastAPI.Test\6_t4IntegrationTestGenerate.tt" -out "..\GWSuncoastAPI.Test\6_t4IntegrationTestGenerate.cs"	
echo T4s completed.
pause
:END