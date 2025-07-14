# CROSS-DOMAIN NAMING ALIGNMENT ANALYSIS

**Analysis Date**: July 14, 2025  
**Purpose**: Ensure perfect C# PascalCase to Python snake_case conversion across all domains  
**Scope**: Device, Memory, Model, Inference, Processing, Postprocessing domains  

---

## Executive Summary

This analysis identifies **critical naming misalignments** across domains that prevent simple PascalCase ↔ snake_case conversion. The inconsistencies fall into three main categories:

1. **🔴 Parameter Naming Inconsistencies**: Mixed `idProperty` vs `propertyId` patterns
2. **🔴 Method Naming Misalignments**: C# methods don't match Python command expectations  
3. **🔴 Domain Operation Mismatches**: Inconsistent operation naming between C# and Python

## Critical Naming Misalignments by Domain

### 1. Device Domain ⚠️ **Parameter Inconsistencies**

**C# Parameter Issues**:
```csharp
// ❌ INCONSISTENT: Mixed parameter naming patterns
GetDeviceAsync(string deviceId)                 // ✅ Correct: propertyId pattern
GetDeviceCapabilitiesAsync(string? deviceId)    // ✅ Correct: propertyId pattern  
GetDeviceStatusAsync(string? deviceId)          // ✅ Correct: propertyId pattern
```

**Status**: ✅ **Already correct** - Device domain uses consistent `propertyId` pattern

### 2. Memory Domain ⚠️ **Parameter Inconsistencies**

**C# Parameter Issues**:
```csharp
// ❌ INCONSISTENT: Mixed parameter patterns
GetMemoryStatusAsync(string deviceId)           // ✅ Correct: propertyId pattern
GetMemoryStatusAsync(string allocationId)       // ✅ Correct: propertyId pattern
DeleteMemoryAllocationAsync(string allocationId)// ✅ Correct: propertyId pattern

// BUT ALSO:
SomeMethod(string idDevice)                     // ❌ Wrong: idProperty pattern
```

**Required Fix**:
```csharp
// STANDARDIZE TO: propertyId pattern consistently
string deviceId      // ✅ Standard
string allocationId  // ✅ Standard
string transferId    // ✅ Standard
```

### 3. Model Domain 🔴 **CRITICAL Parameter Inconsistencies**

**C# Parameter Issues**:
```csharp
// ❌ CRITICAL INCONSISTENCY: Wrong parameter pattern used
GetModel(string idModel)                        // ❌ Wrong: should be modelId
GetModelStatus(string idDevice)                 // ❌ Wrong: should be deviceId
PostModelLoad(string idModel, ...)              // ❌ Wrong: should be modelId
PostModelUnload(string idModel, ...)            // ❌ Wrong: should be modelId
```

**Required C# Parameter Fixes**:
```csharp
// CURRENT (Wrong) → REQUIRED (Correct)
GetModel(string idModel)           → GetModel(string modelId)
GetModelStatus(string idDevice)    → GetModelStatus(string deviceId)  
PostModelLoad(string idModel, ...) → PostModelLoad(string modelId, ...)
PostModelUnload(string idModel, ...)→ PostModelUnload(string modelId, ...)
```

**Python Command Misalignments**:
```python
# ❌ CURRENT (Misaligned) → ✅ REQUIRED (Aligned)
"model.get_model_info"    → "model.get_model"
"model.optimize_memory"   → "model.post_model_optimize"

# ❌ MISSING (Need Implementation)
"model.post_model_validate"
"model.post_model_benchmark" 
"model.get_model_metadata"
"model.put_model_metadata"
```

### 4. Inference Domain ✅ **Excellent Alignment**

**Status**: **Perfect naming alignment** - no changes needed
- Parameter naming: Consistent `idDevice`, `idSession` pattern
- Field transformation: 60+ explicit mappings working perfectly
- Operation alignment: Perfect C# → Python mapping

### 5. Processing Domain ⚠️ **Parameter Inconsistencies**

**C# Parameter Issues**:
```csharp
// ❌ INCONSISTENT: Mixed parameter patterns
string idSession    → sessionId (inconsistent response naming)
string idBatch      → batchId (inconsistent response naming)  
string idWorkflow   → workflowId (inconsistent response naming)
```

**Required Processing Parameter Standardization**:
```csharp
// STANDARDIZE TO: propertyId pattern
GetProcessingSession(string sessionId)     // ✅ Standard
GetProcessingBatch(string batchId)         // ✅ Standard
GetProcessingWorkflow(string workflowId)   // ✅ Standard
```

### 6. Postprocessing Domain ✅ **Excellent Alignment**

**Status**: **Perfect naming alignment** - no changes needed
- Parameter naming: Consistent patterns throughout
- Field transformation: 70+ explicit mappings working perfectly
- Operation alignment: Excellent C# → Python mapping

---

## Required Cross-Domain Standardization

### **Standard 1: Parameter Naming Convention**

**✅ ADOPT STANDARD**: `propertyId` pattern for all domains
```csharp
// STANDARD PATTERN (use consistently across ALL domains)
string deviceId        // ✅ Standard: device + Id
string modelId         // ✅ Standard: model + Id  
string sessionId       // ✅ Standard: session + Id
string batchId         // ✅ Standard: batch + Id
string workflowId      // ✅ Standard: workflow + Id
string allocationId    // ✅ Standard: allocation + Id
string transferId      // ✅ Standard: transfer + Id

// ❌ AVOID PATTERN: idProperty (inconsistent conversion)
string idDevice        // ❌ Avoid: id + Device
string idModel         // ❌ Avoid: id + Model
string idSession       // ❌ Avoid: id + Session
```

**Conversion Result**:
```csharp
// C# PascalCase → Python snake_case (PERFECT CONVERSION)
deviceId    → device_id      // ✅ Perfect conversion
modelId     → model_id       // ✅ Perfect conversion
sessionId   → session_id     // ✅ Perfect conversion

// vs. problematic conversion
idDevice    → id_device      // ❌ Awkward conversion
idModel     → id_model       // ❌ Awkward conversion
```

### **Standard 2: Method Naming Alignment**

**C# Service Method → Python Command Mapping**:
```csharp
// STANDARD PATTERN: {HttpVerb}{Domain}{Operation}Async
GetModelAsync(modelId)           → "model.get_model"
PostModelLoadAsync(modelId, ...) → "model.post_model_load"
PostModelValidateAsync(...)      → "model.post_model_validate"
```

### **Standard 3: Field Naming Consistency**

**Request/Response Field Conversion**:
```csharp
// C# Property → Python Field (STANDARD CONVERSION)
public string DeviceId { get; set; }     → "device_id"
public string ModelId { get; set; }      → "model_id"  
public string RequestId { get; set; }    → "request_id"
public int ProcessingTimeMs { get; set; } → "processing_time_ms"
```

---

## Domain-Specific Fixes Required

### Model Domain Fixes 🔴 **CRITICAL**

**File: `src/Controllers/ControllerModel.cs`**
```csharp
// CURRENT (Wrong) → REQUIRED (Fixed)
[HttpGet("{idModel}")]
public async Task<ActionResult<GetModelResponse>> GetModel(string idModel)
    → public async Task<ActionResult<GetModelResponse>> GetModel(string modelId)

[HttpPost("{idModel}/load")]  
public async Task<ActionResult<PostModelLoadResponse>> PostModelLoad(string idModel, ...)
    → public async Task<ActionResult<PostModelLoadResponse>> PostModelLoad(string modelId, ...)
```

**File: `src/Services/Model/ServiceModel.cs`**
```csharp
// CURRENT (Wrong) → REQUIRED (Fixed)
public async Task<ApiResponse<GetModelResponse>> GetModelAsync(string idModel)
    → public async Task<ApiResponse<GetModelResponse>> GetModelAsync(string modelId)

public async Task<ApiResponse<PostModelLoadResponse>> PostModelLoadAsync(string idModel, ...)
    → public async Task<ApiResponse<PostModelLoadResponse>> PostModelLoadAsync(string modelId, ...)
```

**File: `src/Workers/instructors/instructor_model.py`**
```python
# CURRENT (Misaligned) → REQUIRED (Aligned)
"model.get_model_info"    → "model.get_model"
"model.optimize_memory"   → "model.post_model_optimize"

# ADD MISSING HANDLERS:
"model.post_model_validate"
"model.post_model_benchmark"
"model.get_model_metadata"
"model.put_model_metadata"
```

### Memory Domain Fixes ⚠️ **Medium Priority**

**File: `src/Services/Memory/ServiceMemory.cs`**
```csharp
// ENSURE CONSISTENT: propertyId pattern throughout
// Review all parameter names for consistency with deviceId, allocationId patterns
```

### Processing Domain Fixes ⚠️ **Medium Priority**

**File: `src/Controllers/ControllerProcessing.cs`**
```csharp
// STANDARDIZE: parameter names for consistency
// Ensure sessionId, batchId, workflowId used consistently
```

---

## Implementation Plan for Naming Alignment

### Phase 1: Model Domain Critical Fixes (Week 1)

**Priority**: 🔴 **CRITICAL** - Breaks PascalCase ↔ snake_case conversion

1. **Update C# Parameter Names**:
   - `ControllerModel.cs`: Change all `idModel` → `modelId`, `idDevice` → `deviceId`
   - `ServiceModel.cs`: Update all method signatures to use `modelId`, `deviceId`
   - `IServiceModel.cs`: Update interface to match implementation

2. **Update Python Command Alignment**:
   - `instructor_model.py`: Align command names with C# operations
   - `interface_model.py`: Add missing method implementations
   - Test all command routing for consistency

3. **Validation**:
   - Test field transformation with new parameter names
   - Verify PascalCase → snake_case conversion works perfectly
   - Validate all model operations work end-to-end

### Phase 2: Memory & Processing Domain Consistency (Week 2)

**Priority**: ⚠️ **Medium** - Consistency improvement

1. **Memory Domain Standardization**:
   - Review all parameter names for `propertyId` pattern consistency
   - Update any remaining `idProperty` patterns

2. **Processing Domain Standardization**:
   - Standardize parameter naming across all processing operations
   - Ensure response field naming matches parameter patterns

### Phase 3: Validation & Testing (Week 3)

**Priority**: ✅ **Validation** - Ensure perfection

1. **Cross-Domain Testing**:
   - Test PascalCase ↔ snake_case conversion across all domains
   - Validate field transformers work with standardized naming
   - Performance test naming conversion overhead

2. **Documentation Update**:
   - Update all domain Phase 3 analyses with fixes
   - Document naming standards for future development
   - Create naming validation guidelines

---

## Expected Outcomes

### Before Fixes
- **Model Domain**: Broken PascalCase ↔ snake_case conversion (`idModel` → `id_model`)
- **Memory Domain**: Mixed parameter patterns causing confusion
- **Processing Domain**: Inconsistent parameter naming across operations
- **Cross-Domain**: Different naming patterns across domains

### After Fixes
- **Perfect Conversion**: All domains use consistent `propertyId` → `property_id` conversion
- **Simple Field Transformation**: Automatic PascalCase ↔ snake_case conversion works perfectly
- **Consistent API**: All domains follow same parameter naming patterns
- **Maintainable Code**: Clear, consistent naming reduces development confusion

---

## Success Metrics

### Naming Consistency Metrics
- **Parameter Pattern Compliance**: 100% `propertyId` pattern usage
- **Field Conversion Accuracy**: 100% automatic PascalCase ↔ snake_case success  
- **Cross-Domain Consistency**: Same patterns across all 6 domains
- **Python Command Alignment**: Perfect C# operation → Python command mapping

### Quality Improvements
- **Developer Experience**: Consistent naming reduces cognitive load
- **Maintainability**: Clear patterns make code easier to understand
- **Field Transformation**: Simplified automatic conversion logic
- **API Consistency**: Uniform parameter naming across all domains

---

**Status**: Analysis complete, implementation plan ready  
**Next Action**: Begin Model Domain critical fixes (highest priority for PascalCase ↔ snake_case conversion)
