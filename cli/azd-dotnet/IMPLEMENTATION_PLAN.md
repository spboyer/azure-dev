# Azure Developer CLI - .NET 10 Conversion Implementation Plan

**Status**: Week 2 Complete - Core Services (15-20%)  
**Remaining**: 80-85% of functionality  
**Target Timeline**: 14-21 weeks  
**Last Updated**: November 14, 2025

---

## Phase 1: Fix Compilation & Core Services (Weeks 1-4)

### Week 1: Compilation & Configuration

**Priority**: CRITICAL  
**Effort**: 2-3 days  
**Owner**: Platform Team

#### Tasks

1. **Fix Package References** (Day 1) ✅
   - [x] Add missing Azure.ResourceManager packages
   - [x] Fix Azure.Identity version conflicts
   - [x] Update System.CommandLine to stable version if available
   - [x] Test all projects compile without errors
   - [x] Run `dotnet build` successfully

2. **Configuration Service** (Days 2-3) ✅
   - [x] Port `pkg/config/config.go`
   - [x] Implement user config directory (`~/.azd/`)
   - [x] File-based config persistence (JSON)
   - [x] Environment variable support
   - [x] Config get/set/unset/list operations
   - [x] Unit tests

   **Files Created**:
   - `src/azd.Core/Services/IConfigService.cs` ✅
   - `src/azd.Core/Services/ConfigService.cs` ✅
   - `tests/azd.Tests/ConfigServiceTests.cs` ✅

   **Go Reference**: `cli/azd/pkg/config/`

### Week 2: Project & Environment Management ✅

**Priority**: CRITICAL  
**Effort**: 5 days  
**Owner**: Core Team  
**Status**: Complete

#### Tasks

1. **Project Service Implementation** (Days 1-3) ✅
   - [x] Port `pkg/project/project_config.go`
   - [x] YAML parser for `azure.yaml` (YamlDotNet)
   - [x] Load project configuration
   - [x] Save project configuration
   - [x] Validate project structure
   - [x] Service detection
   - [x] Unit tests with sample azure.yaml files

   **Files Created**:
   - `src/azd.Core/Models/ProjectConfig.cs` (significantly expanded) ✅
   - `src/azd.Core/Services/ProjectService.cs` ✅
   - `src/azd.Core/Parsers/AzureYamlParser.cs` ✅
   - `src/azd.Core/Validation/ProjectValidator.cs` ✅
   - `tests/azd.Tests/ProjectServiceTests.cs` ✅
   - `tests/azd.Tests/TestData/simple-azure.yaml` ✅
   - `tests/azd.Tests/TestData/complex-azure.yaml` ✅
   - `tests/azd.Tests/TestData/multiservice-azure.yaml` ✅

   **Go Reference**: `cli/azd/pkg/project/project_config.go`

2. **Environment Management** (Days 4-5) ✅
   - [x] Port `pkg/environment/` package
   - [x] Environment creation (`azd env new`)
   - [x] Environment selection (`azd env select`)
   - [x] Environment listing (`azd env list`)
   - [x] Environment values (`azd env get-values`)
   - [x] .env file handling
   - [x] Environment secrets management
   - [x] Unit tests

   **Files Created**:
   - `src/azd.Core/Services/IEnvironmentService.cs` ✅
   - `src/azd.Core/Services/EnvironmentService.cs` ✅
   - `src/azd.Core/Storage/EnvironmentStore.cs` ✅
   - `src/azd/Commands/EnvCommands.cs` ✅
   - `tests/azd.Tests/EnvironmentServiceTests.cs` ✅

   **Go Reference**: `cli/azd/pkg/environment/`

### Week 3: Command Implementation - Part 1

**Priority**: HIGH  
**Effort**: 5 days  
**Owner**: CLI Team

#### Tasks

1. **Version Command** (Day 1)
   - [ ] Port `cmd/version.go`
   - [ ] Display version information
   - [ ] Show commit SHA
   - [ ] Show build date
   - [ ] Unit tests

   **Files to Update**:
   - `src/azd/Commands/CommandHandlers.cs`
   - Add `VersionCommandHandler.cs`

   **Go Reference**: `cli/azd/cmd/version.go`

2. **Auth Commands** (Days 2-3)
   - [ ] Port `cmd/auth*.go` files
   - [ ] `azd auth login` - Interactive browser flow
   - [ ] `azd auth logout` - Clear credentials
   - [ ] `azd auth token` - Get access token
   - [ ] Token caching with MSAL
   - [ ] Device code flow support
   - [ ] Service principal support
   - [ ] Unit tests with mocked auth

   **Files to Create**:
   - `src/azd/Commands/AuthCommands.cs`
   - `src/azd.Auth/Providers/InteractiveAuthProvider.cs`
   - `src/azd.Auth/Providers/ServicePrincipalProvider.cs`
   - `src/azd.Auth/TokenCache/TokenCacheService.cs`
   - `tests/azd.Tests/AuthCommandTests.cs`

   **Go Reference**: `cli/azd/cmd/auth*.go`

3. **Config Commands** (Days 4-5)
   - [ ] Port `cmd/config.go`
   - [ ] `azd config get` implementation
   - [ ] `azd config set` implementation
   - [ ] `azd config unset` implementation
   - [ ] `azd config list` implementation
   - [ ] User vs workspace config scopes
   - [ ] Unit tests

   **Files to Create**:
   - `src/azd/Commands/ConfigCommands.cs`
   - `tests/azd.Tests/ConfigCommandTests.cs`

   **Go Reference**: `cli/azd/cmd/config.go`

### Week 4: Service Detection & App Detection

**Priority**: HIGH  
**Effort**: 5 days  
**Owner**: Detection Team

#### Tasks

1. **App Detection Framework** (Days 1-3)
   - [ ] Port `appdetect/` package
   - [ ] Language detection (.NET, Node.js, Python, Java, Go)
   - [ ] Framework detection (ASP.NET, Express, Flask, Spring)
   - [ ] Docker detection (Dockerfile presence)
   - [ ] Database detection (connection strings, config)
   - [ ] Unit tests with sample projects

   **Files to Create**:
   - `src/azd.Core/Detection/ILanguageDetector.cs`
   - `src/azd.Core/Detection/DotNetDetector.cs`
   - `src/azd.Core/Detection/NodeDetector.cs`
   - `src/azd.Core/Detection/PythonDetector.cs`
   - `src/azd.Core/Detection/JavaDetector.cs`
   - `src/azd.Core/Detection/DockerDetector.cs`
   - `tests/azd.Tests/Detection/`

   **Go Reference**: `cli/azd/appdetect/`

2. **Service Configuration** (Days 4-5)
   - [ ] Port `pkg/project/service_config.go`
   - [ ] Service target resolution (appservice, containerapp, aks)
   - [ ] Service dependency analysis
   - [ ] Service ordering for deployment
   - [ ] Unit tests

   **Files to Update**:
   - `src/azd.Core/Models/ServiceConfig.cs` (enhance)
   - `src/azd.Core/Services/ServiceResolver.cs` (new)

   **Go Reference**: `cli/azd/pkg/project/service_config.go`

---

## Phase 2: Infrastructure Provisioning (Weeks 5-7)

### Week 5: Bicep Provider Implementation

**Priority**: CRITICAL  
**Effort**: 5 days  
**Owner**: Infrastructure Team

#### Tasks

1. **Bicep CLI Integration** (Days 1-2)
   - [ ] Bicep CLI detection and version check
   - [ ] Bicep compile (`az bicep build`)
   - [ ] Bicep linting
   - [ ] Error handling and reporting
   - [ ] Process execution helpers

   **Files to Create**:
   - `src/azd.Infra/Tools/BicepCli.cs`
   - `src/azd.Infra/Tools/ICliTool.cs`
   - `src/azd.Infra/Tools/ProcessExecutor.cs`

   **Go Reference**: `cli/azd/pkg/tools/bicep/`

2. **ARM Deployment** (Days 3-5)
   - [ ] Port `pkg/infra/provisioning/bicep/bicep.go`
   - [ ] ARM template deployment
   - [ ] Parameter file handling
   - [ ] Deployment progress tracking
   - [ ] Deployment output extraction
   - [ ] Error handling with detailed messages
   - [ ] What-if operation
   - [ ] Integration tests

   **Files to Update**:
   - `src/azd.Infra/Provisioning/BicepProvider.cs` (complete)
   - `src/azd.Infra/Provisioning/ArmDeploymentService.cs` (new)

   **Go Reference**: `cli/azd/pkg/infra/provisioning/bicep/`

### Week 6: Terraform Provider

**Priority**: MEDIUM  
**Effort**: 5 days  
**Owner**: Infrastructure Team

#### Tasks

1. **Terraform CLI Integration** (Days 1-2)
   - [ ] Port `pkg/tools/terraform/`
   - [ ] Terraform CLI detection
   - [ ] Terraform init
   - [ ] Terraform plan
   - [ ] Terraform apply
   - [ ] Terraform destroy
   - [ ] State management

   **Files to Create**:
   - `src/azd.Infra/Tools/TerraformCli.cs`
   - `src/azd.Infra/Provisioning/TerraformProvider.cs`

   **Go Reference**: `cli/azd/pkg/tools/terraform/`

2. **Provider Management** (Days 3-5)
   - [ ] Provider factory pattern
   - [ ] Auto-detection of infra type (Bicep vs Terraform)
   - [ ] Provider configuration
   - [ ] Unit tests

   **Files to Create**:
   - `src/azd.Infra/Provisioning/ProviderFactory.cs`
   - `src/azd.Infra/Provisioning/InfraDetector.cs`

### Week 7: Provision Command

**Priority**: CRITICAL  
**Effort**: 5 days  
**Owner**: CLI Team

#### Tasks

1. **Provision Command Implementation** (Days 1-5)
   - [ ] Port `cmd/provision.go`
   - [ ] Pre-provision hooks execution
   - [ ] Infrastructure provisioning
   - [ ] Post-provision hooks execution
   - [ ] Progress indicators
   - [ ] Output display
   - [ ] Error handling
   - [ ] Integration tests

   **Files to Update**:
   - `src/azd/Commands/ProvisionCommandHandler.cs` (complete)
   - `src/azd.Core/Hooks/HookRunner.cs` (new)

   **Go Reference**: `cli/azd/cmd/provision.go`

---

## Phase 3: Service Deployment (Weeks 8-11)

### Week 8: Docker & Container Support

**Priority**: HIGH  
**Effort**: 5 days  
**Owner**: Deployment Team

#### Tasks

1. **Docker CLI Integration** (Days 1-2)
   - [ ] Port `pkg/tools/docker/`
   - [ ] Docker detection
   - [ ] Docker build
   - [ ] Docker tag
   - [ ] Docker push
   - [ ] Multi-platform builds

   **Files to Create**:
   - `src/azd.Core/Tools/DockerCli.cs`
   - `src/azd.Core/Tools/IContainerTool.cs`

   **Go Reference**: `cli/azd/pkg/tools/docker/`

2. **Container Registry Operations** (Days 3-5)
   - [ ] Azure Container Registry (ACR) integration
   - [ ] Registry authentication
   - [ ] Image push/pull
   - [ ] Repository management

   **Files to Create**:
   - `src/azd.Infra/Azure/ContainerRegistryService.cs`

### Week 9: Service Targets - App Service

**Priority**: HIGH  
**Effort**: 5 days  
**Owner**: Deployment Team

#### Tasks

1. **App Service Deployment** (Days 1-5)
   - [ ] Port `pkg/apphost/` for App Service
   - [ ] ZIP deployment
   - [ ] Docker container deployment
   - [ ] Configuration settings
   - [ ] Connection strings
   - [ ] Application settings
   - [ ] Deployment slots
   - [ ] Integration tests

   **Files to Create**:
   - `src/azd.Infra/Azure/AppServiceDeployment.cs`
   - `src/azd.Infra/Azure/IServiceDeployment.cs`
   - `src/azd.Infra/Packaging/ZipPackager.cs`

   **Go Reference**: `cli/azd/pkg/apphost/`, `cli/azd/pkg/project/framework_service_*.go`

### Week 10: Service Targets - Container Apps

**Priority**: HIGH  
**Effort**: 5 days  
**Owner**: Deployment Team

#### Tasks

1. **Container Apps Deployment** (Days 1-5)
   - [ ] Container Apps service implementation
   - [ ] Container image deployment
   - [ ] Revision management
   - [ ] Environment variables
   - [ ] Secrets management
   - [ ] Ingress configuration
   - [ ] Integration tests

   **Files to Create**:
   - `src/azd.Infra/Azure/ContainerAppsDeployment.cs`

### Week 11: Service Targets - AKS

**Priority**: MEDIUM  
**Effort**: 5 days  
**Owner**: Deployment Team

#### Tasks

1. **AKS Deployment** (Days 1-5)
   - [ ] Port AKS deployment logic
   - [ ] Kubernetes manifest generation
   - [ ] kubectl integration
   - [ ] Helm support
   - [ ] Namespace management
   - [ ] Integration tests

   **Files to Create**:
   - `src/azd.Infra/Azure/AksDeployment.cs`
   - `src/azd.Core/Tools/KubectlCli.cs`
   - `src/azd.Core/Tools/HelmCli.cs`

---

## Phase 4: Build & Package (Weeks 12-13)

### Week 12: Service Packaging

**Priority**: HIGH  
**Effort**: 5 days  
**Owner**: Build Team

#### Tasks

1. **Language-Specific Build** (Days 1-3)
   - [ ] Port `pkg/project/framework_service_*.go`
   - [ ] .NET build (`dotnet publish`)
   - [ ] Node.js build (`npm run build`)
   - [ ] Python build (pip install)
   - [ ] Java build (Maven/Gradle)
   - [ ] Output path resolution

   **Files to Create**:
   - `src/azd.Core/Build/IBuildProvider.cs`
   - `src/azd.Core/Build/DotNetBuildProvider.cs`
   - `src/azd.Core/Build/NodeBuildProvider.cs`
   - `src/azd.Core/Build/PythonBuildProvider.cs`
   - `src/azd.Core/Build/JavaBuildProvider.cs`

   **Go Reference**: `cli/azd/pkg/project/framework_service_*.go`

2. **Package Command** (Days 4-5)
   - [ ] Port `cmd/package.go`
   - [ ] Service packaging orchestration
   - [ ] Docker image building
   - [ ] ZIP file creation
   - [ ] Pre-package hooks
   - [ ] Post-package hooks

   **Files to Update**:
   - `src/azd/Commands/PackageCommandHandler.cs` (new)

   **Go Reference**: `cli/azd/cmd/package.go`

### Week 13: Deploy Command

**Priority**: CRITICAL  
**Effort**: 5 days  
**Owner**: Deployment Team

#### Tasks

1. **Deploy Command Implementation** (Days 1-5)
   - [ ] Port `cmd/deploy.go`
   - [ ] Service deployment orchestration
   - [ ] Parallel deployment support
   - [ ] Dependency ordering
   - [ ] Pre-deploy hooks
   - [ ] Post-deploy hooks
   - [ ] Deployment status tracking
   - [ ] Error handling and rollback
   - [ ] Integration tests

   **Files to Update**:
   - `src/azd/Commands/DeployCommandHandler.cs` (complete)

   **Go Reference**: `cli/azd/cmd/deploy.go`

---

## Phase 5: Templates & Init (Weeks 14-16)

### Week 14: Template System

**Priority**: HIGH  
**Effort**: 5 days  
**Owner**: Templates Team

#### Tasks

1. **Template Management** (Days 1-3)
   - [ ] Port `pkg/templates/`
   - [ ] Template download (Git clone)
   - [ ] Template caching (`~/.azd/templates/`)
   - [ ] Template listing
   - [ ] Template metadata parsing
   - [ ] GitHub repository detection

   **Files to Create**:
   - `src/azd.Core/Templates/ITemplateService.cs`
   - `src/azd.Core/Templates/TemplateService.cs`
   - `src/azd.Core/Templates/TemplateCache.cs`
   - `src/azd.Core/Templates/TemplateMetadata.cs`

   **Go Reference**: `cli/azd/pkg/templates/`

2. **Template Instantiation** (Days 4-5)
   - [ ] Variable substitution
   - [ ] File processing
   - [ ] Directory structure creation
   - [ ] Post-init scripts

   **Files to Create**:
   - `src/azd.Core/Templates/TemplateProcessor.cs`
   - `src/azd.Core/Templates/VariableSubstitutor.cs`

### Week 15: Init Command

**Priority**: CRITICAL  
**Effort**: 5 days  
**Owner**: CLI Team

#### Tasks

1. **Init Command Implementation** (Days 1-5)
   - [ ] Port `cmd/init.go`
   - [ ] Interactive prompts
   - [ ] Template selection
   - [ ] Project initialization
   - [ ] Environment creation
   - [ ] Git initialization
   - [ ] Initial provisioning option
   - [ ] Integration tests

   **Files to Update**:
   - `src/azd/Commands/InitCommandHandler.cs` (complete)
   - `src/azd.Core/Prompts/IPromptService.cs` (new)

   **Go Reference**: `cli/azd/cmd/init.go`

### Week 16: Up & Down Commands

**Priority**: CRITICAL  
**Effort**: 5 days  
**Owner**: CLI Team

#### Tasks

1. **Up Command** (Days 1-3)
   - [ ] Port `cmd/up.go`
   - [ ] Provision + Deploy orchestration
   - [ ] Progress tracking
   - [ ] Error handling
   - [ ] Integration tests

   **Files to Update**:
   - `src/azd/Commands/UpCommandHandler.cs` (complete)

   **Go Reference**: `cli/azd/cmd/up.go`

2. **Down Command** (Days 4-5)
   - [ ] Port `cmd/down.go`
   - [ ] Resource deletion
   - [ ] Confirmation prompts
   - [ ] Purge option
   - [ ] Integration tests

   **Files to Update**:
   - `src/azd/Commands/DownCommandHandler.cs` (complete)

   **Go Reference**: `cli/azd/cmd/down.go`

---

## Phase 6: CI/CD & Extensions (Weeks 17-18)

### Week 17: Pipeline Configuration

**Priority**: MEDIUM  
**Effort**: 5 days  
**Owner**: Pipeline Team

#### Tasks

1. **GitHub Actions Support** (Days 1-3)
   - [ ] Port `pkg/pipeline/github_*.go`
   - [ ] Workflow file generation
   - [ ] Service principal setup
   - [ ] Federated identity configuration
   - [ ] Secret configuration

   **Files to Create**:
   - `src/azd.Core/Pipeline/IGitHubService.cs`
   - `src/azd.Core/Pipeline/GitHubActionsService.cs`
   - `src/azd.Core/Pipeline/WorkflowGenerator.cs`

   **Go Reference**: `cli/azd/pkg/pipeline/github*.go`

2. **Azure DevOps Support** (Days 4-5)
   - [ ] Port `pkg/pipeline/azdo_*.go`
   - [ ] Pipeline YAML generation
   - [ ] Service connection setup
   - [ ] Variable group configuration

   **Files to Create**:
   - `src/azd.Core/Pipeline/IAzureDevOpsService.cs`
   - `src/azd.Core/Pipeline/AzureDevOpsService.cs`

   **Go Reference**: `cli/azd/pkg/pipeline/azdo*.go`

### Week 18: Hooks & Extensions

**Priority**: MEDIUM  
**Effort**: 5 days  
**Owner**: Extensions Team

#### Tasks

1. **Hooks System** (Days 1-3)
   - [ ] Port `pkg/project/hooks.go`
   - [ ] Pre/post hook execution
   - [ ] Shell script execution
   - [ ] PowerShell execution
   - [ ] Hook context and variables
   - [ ] Error handling

   **Files to Create**:
   - `src/azd.Core/Hooks/HookExecutor.cs`
   - `src/azd.Core/Hooks/HookContext.cs`
   - `src/azd.Core/Shell/ShellExecutor.cs`

   **Go Reference**: `cli/azd/pkg/project/hooks.go`

2. **Extension System** (Days 4-5)
   - [ ] Port `pkg/extensions/`
   - [ ] Extension loading
   - [ ] Extension manifest parsing
   - [ ] Extension execution

   **Files to Create**:
   - `src/azd.Core/Extensions/IExtensionService.cs`
   - `src/azd.Core/Extensions/ExtensionLoader.cs`

   **Go Reference**: `cli/azd/pkg/extensions/`

---

## Phase 7: Monitoring & Additional Features (Weeks 19-20)

### Week 19: Monitoring Commands

**Priority**: LOW  
**Effort**: 5 days  
**Owner**: Monitoring Team

#### Tasks

1. **Monitor Command** (Days 1-3)
   - [ ] Port `cmd/monitor.go`
   - [ ] Application Insights integration
   - [ ] Log streaming
   - [ ] Metrics display
   - [ ] Dashboard URL generation

   **Files to Create**:
   - `src/azd/Commands/MonitorCommands.cs`
   - `src/azd.Infra/Azure/ApplicationInsightsService.cs`

   **Go Reference**: `cli/azd/cmd/monitor.go`

2. **Restore & Show Commands** (Days 4-5)
   - [ ] Port `cmd/restore.go`
   - [ ] Port `cmd/show.go`
   - [ ] Dependency restoration
   - [ ] Resource display

   **Files to Create**:
   - `src/azd/Commands/RestoreCommandHandler.cs`
   - `src/azd/Commands/ShowCommands.cs`

### Week 20: Additional Tools

**Priority**: LOW  
**Effort**: 5 days  
**Owner**: Tools Team

#### Tasks

1. **Azure CLI Integration** (Days 1-2)
   - [ ] Port `pkg/tools/azcli/`
   - [ ] Azure CLI detection
   - [ ] Azure CLI command execution
   - [ ] Account management

   **Files to Create**:
   - `src/azd.Core/Tools/AzureCli.cs`

   **Go Reference**: `cli/azd/pkg/tools/azcli/`

2. **Additional Tools** (Days 3-5)
   - [ ] Port `pkg/tools/github/`
   - [ ] Port `pkg/tools/dotnet/`
   - [ ] Port `pkg/tools/npm/`
   - [ ] Port `pkg/tools/python/`

   **Files to Create**:
   - Various tool wrappers in `src/azd.Core/Tools/`

---

## Phase 8: Testing & Polish (Weeks 21-23)

### Week 21: Unit Test Completion

**Priority**: CRITICAL  
**Effort**: 5 days  
**Owner**: QA Team

#### Tasks

1. **Test Coverage** (Days 1-5)
   - [ ] Port remaining unit tests from Go
   - [ ] Achieve >80% code coverage
   - [ ] Add integration tests
   - [ ] Add end-to-end tests

   **Target**: 500+ unit tests, 50+ integration tests

### Week 22: Integration Testing

**Priority**: HIGH  
**Effort**: 5 days  
**Owner**: QA Team

#### Tasks

1. **End-to-End Scenarios** (Days 1-5)
   - [ ] Full workflow tests (init → up → down)
   - [ ] Multi-service scenarios
   - [ ] Template instantiation tests
   - [ ] CI/CD pipeline tests
   - [ ] Error scenario tests

### Week 23: Performance & Polish

**Priority**: MEDIUM  
**Effort**: 5 days  
**Owner**: Platform Team

#### Tasks

1. **Performance Optimization** (Days 1-3)
   - [ ] Startup time optimization
   - [ ] Memory usage profiling
   - [ ] Async/await optimization
   - [ ] Lazy loading
   - [ ] Native AOT optimization

2. **UX Polish** (Days 4-5)
   - [ ] Progress indicators
   - [ ] Spinner animations
   - [ ] Color output
   - [ ] Error messages
   - [ ] Help text

---

## Phase 9: Release Preparation (Week 24)

### Week 24: Build & Release

**Priority**: CRITICAL  
**Effort**: 5 days  
**Owner**: Release Team

#### Tasks

1. **CI/CD Pipeline** (Days 1-3)
   - [ ] Port `eng/pipelines/` configuration
   - [ ] Multi-platform builds (Linux, Windows, macOS)
   - [ ] Native AOT builds
   - [ ] Package generation (MSI, DEB, RPM, Homebrew)
   - [ ] Automated testing
   - [ ] Release automation

   **Files to Create**:
   - `.github/workflows/build-dotnet.yml`
   - `eng/build-dotnet.ps1`
   - `eng/build-dotnet.sh`

2. **Documentation** (Days 4-5)
   - [ ] Update main README
   - [ ] Migration guide
   - [ ] Breaking changes documentation
   - [ ] Performance comparison
   - [ ] Release notes

---

## Dependency Graph

```
Phase 1 (Core)
    ↓
Phase 2 (Infrastructure) → Phase 3 (Deployment)
    ↓                           ↓
Phase 4 (Build/Package) ←------┘
    ↓
Phase 5 (Templates/Init)
    ↓
Phase 6 (CI/CD/Extensions)
    ↓
Phase 7 (Monitoring/Tools)
    ↓
Phase 8 (Testing)
    ↓
Phase 9 (Release)
```

---

## Critical Path

The critical path for MVP functionality:

1. **Week 1**: Fix compilation + Config ✓
2. **Week 2**: Project + Environment ✓
3. **Week 5-7**: Bicep provisioning ✓
4. **Week 8-11**: Service deployment ✓
5. **Week 12-13**: Build + Deploy ✓
6. **Week 14-16**: Templates + Init + Up/Down ✓
7. **Week 21-24**: Testing + Release ✓

**Minimum Viable Product**: 16 weeks (Phases 1-5 + selective Phase 8-9)

---

## Resource Allocation

### Team Structure

- **Platform Team** (2 engineers): Core services, configuration
- **Infrastructure Team** (2 engineers): Bicep, Terraform, provisioning
- **Deployment Team** (2 engineers): Service deployment, targets
- **CLI Team** (2 engineers): Command implementations
- **Templates Team** (1 engineer): Template system
- **Pipeline Team** (1 engineer): CI/CD integration
- **QA Team** (2 engineers): Testing, automation
- **Total**: 12 engineers

### Alternative: Smaller Team

With 4-6 engineers, extend timeline to 24-30 weeks.

---

## Risk Mitigation

### High Risk Items

1. **Azure SDK Compatibility**
   - Risk: Some Azure services may not have .NET SDK parity with Go
   - Mitigation: Identify gaps early, use ARM REST APIs if needed

2. **Native AOT Limitations**
   - Risk: Not all libraries support Native AOT
   - Mitigation: Test early, have fallback to regular .NET runtime

3. **Performance Regression**
   - Risk: .NET may be slower than Go for some operations
   - Mitigation: Profile continuously, optimize hot paths

4. **Breaking Changes**
   - Risk: API changes may break existing user workflows
   - Mitigation: Maintain compatibility layer, clear migration guide

### Medium Risk Items

1. **Testing Coverage**
   - Risk: Incomplete test migration
   - Mitigation: Parallel test development, automated coverage tracking

2. **Documentation Drift**
   - Risk: Docs not updated during conversion
   - Mitigation: Documentation as part of each phase

---

## Success Metrics

### Phase Completion Criteria

Each phase must meet:
- ✓ All features implemented
- ✓ Unit tests passing (>80% coverage)
- ✓ Integration tests passing
- ✓ Documentation updated
- ✓ Code review completed
- ✓ Performance benchmarks met

### Final Release Criteria

- ✓ Feature parity with Go version
- ✓ All commands functional
- ✓ 1000+ unit tests passing
- ✓ 100+ integration tests passing
- ✓ Performance within 20% of Go version
- ✓ Binary size < 100MB
- ✓ Startup time < 500ms
- ✓ Documentation complete
- ✓ Migration guide available

---

## Quick Reference: File Mapping

| Go Package | .NET Project | Priority |
|-----------|--------------|----------|
| `cli/azd/cmd/` | `src/azd/Commands/` | CRITICAL |
| `cli/azd/pkg/project/` | `src/azd.Core/Services/` | CRITICAL |
| `cli/azd/pkg/infra/` | `src/azd.Infra/` | CRITICAL |
| `cli/azd/pkg/auth/` | `src/azd.Auth/` | CRITICAL |
| `cli/azd/pkg/config/` | `src/azd.Core/Services/` | HIGH |
| `cli/azd/pkg/environment/` | `src/azd.Core/Services/` | HIGH |
| `cli/azd/pkg/templates/` | `src/azd.Core/Templates/` | HIGH |
| `cli/azd/pkg/tools/` | `src/azd.Core/Tools/` | HIGH |
| `cli/azd/pkg/pipeline/` | `src/azd.Core/Pipeline/` | MEDIUM |
| `cli/azd/pkg/extensions/` | `src/azd.Core/Extensions/` | MEDIUM |
| `cli/azd/appdetect/` | `src/azd.Core/Detection/` | HIGH |
| `cli/azd/internal/` | `src/azd/Internal/` | LOW |

---

## Getting Started with Implementation

### For Each Phase:

1. **Read Go Code**: Review the referenced Go files
2. **Design .NET API**: Plan the C# equivalent
3. **Create Interfaces**: Define abstractions first
4. **Implement Core**: Write the implementation
5. **Write Tests**: Unit tests alongside code
6. **Integration Test**: End-to-end scenarios
7. **Document**: Update README and docs
8. **Review**: Code review and refinement

### Example Workflow (Week 2, Day 1):

```bash
# 1. Read Go implementation
code cli/azd/pkg/project/project_config.go

# 2. Create interface
code cli/azd-dotnet/src/azd.Core/Services/IProjectService.cs

# 3. Create implementation
code cli/azd-dotnet/src/azd.Core/Services/ProjectService.cs

# 4. Write tests
code cli/azd-dotnet/tests/azd.Tests/ProjectServiceTests.cs

# 5. Test
cd cli/azd-dotnet
dotnet test

# 6. Document
# Update README.md with progress
```

---

## Contact & Support

**Project Lead**: TBD  
**Tech Lead**: TBD  
**Slack Channel**: #azd-dotnet-conversion  
**Weekly Sync**: Thursdays 10am PT  
**Planning Docs**: [Link to project board]

---

**Last Updated**: November 14, 2025  
**Next Review**: Week 1 completion  
**Version**: 1.0
