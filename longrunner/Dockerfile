from mcr.microsoft.com/dotnet/runtime:6.0
COPY bin/Release/net6.0/publish/ App/
WORKDIR /App
ENTRYPOINT ["dotnet", "longrunner.dll"]
