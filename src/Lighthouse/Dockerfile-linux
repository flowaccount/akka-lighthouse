FROM mcr.microsoft.com/dotnet/sdk:5.0-focal AS base

WORKDIR /app
# WORKDIR /mnt/d/projects/opensource/lighthouse/src/Lighthouse
# Install Petabridge.Cmd client so it can be invoked remotely via
# Docker or K8s 'exec` commands

RUN dotnet tool install --global pbm 
COPY ./ ./
RUN dotnet build "./Lighthouse.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:5.0-focal AS app
WORKDIR /app

COPY --from=base ./app/publish /app

# copy .NET Core global tool
COPY --from=base /root/.dotnet /root/.dotnet/

# Needed because https://stackoverflow.com/questions/51977474/install-dotnet-core-tool-dockerfile
ENV PATH="${PATH}:/root/.dotnet/tools"

# should be a comma-delimited list
ENV CLUSTER_SEEDS "[]"
ENV CLUSTER_IP ""
ENV CLUSTER_PORT "4053"
ENV AKKA__CLUSTER__SPLIT_BRAIN_RESOLVER__ACTIVE_STRATEGY "keep-majority"
ENV AKKA__REMOTE__DOT-NETTY__TCP__BATCHING__ENABLED "false"

# 9110 - Petabridge.Cmd

# 4053 - Akka.Cluster
EXPOSE 9110 4053
CMD ["dotnet", "Lighthouse.dll"]