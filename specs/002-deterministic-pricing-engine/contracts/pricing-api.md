# Pricing API Contracts

**Feature**: 002-deterministic-pricing-engine  
**Date**: 2026-02-22  
**Base Path**: `/api/v1/pricing`

## Endpoints

### POST /api/v1/pricing/calculate

Calculate price for a manufacturing job.

**Request Body**:

```json
{
  "fileId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "customerId": "3fa85f64-5717-4562-b3fc-2c963f66afa7",
  "materialId": "3fa85f64-5717-4562-b3fc-2c963f66afa8",
  "technology": "Fdm",
  "layerHeightMm": 0.2,
  "supportEnabled": false,
  "heightMm": 100.0,
  "scanningTier": null,
  "quantity": 1,
  "geometry": {
    "volumeCm3": 10.0,
    "supportVolumeCm3": 0.0,
    "surfaceAreaCm2": 50.0,
    "boundingBoxX": 100.0,
    "boundingBoxY": 100.0,
    "boundingBoxZ": 100.0,
    "isManifold": true,
    "triangleCount": 1000
  },
  "correlationId": "optional-correlation-id"
}
```

**Response (200 OK)**:

```json
{
  "strategy": "RuleBased",
  "materialCost": 7.44,
  "supportMaterialCost": 0.0,
  "machineTimeCost": 12.50,
  "setupCost": 50.0,
  "complexitySurcharge": 0.0,
  "subtotalBeforeMargin": 69.94,
  "marginAmount": 0.0,
  "totalUnitPrice": 300.0,
  "totalPrice": 300.0,
  "confidenceLevel": 1.0,
  "validUntil": "2026-03-24T00:00:00Z",
  "calculationDuration": "00:00:00.0012345",
  "notes": null
}
```

**Error Response (400 Bad Request)**:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Validation Failed",
  "status": 400,
  "detail": "Technology is required for pricing calculation.",
  "errors": {
    "Technology": ["The Technology field is required."]
  }
}
```

**Error Response (502 Bad Gateway)**:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.3",
  "title": "Upstream Service Unavailable",
  "status": 502,
  "detail": "Material service unavailable. Cannot retrieve material properties for MaterialId: 3fa85f64-5717-4562-b3fc-2c963f66afa8"
}
```

---

## Enums

### ManufacturingTechnology

| Value | Name | Description |
|-------|------|-------------|
| 1 | Fdm | Fused Deposition Modeling |
| 2 | Sla | Stereolithography |
| 3 | Cnc | CNC Machining |
| 4 | Scanning | 3D Scanning Service |
| 5 | Design | 3D Design Service |

### ScanningTier (string)

| Value | Description | Minimum Price |
|-------|-------------|---------------|
| RawScan | Raw STL scan only | 2,500 THB |
| ReverseEngineering | Scan + CAD model creation | 4,500 THB |

---

## Request Schema (JSON Schema)

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "required": ["fileId", "customerId", "materialId", "technology", "geometry"],
  "properties": {
    "fileId": { "type": "string", "format": "uuid" },
    "customerId": { "type": "string", "format": "uuid" },
    "materialId": { "type": "string", "format": "uuid" },
    "technology": { 
      "type": "string", 
      "enum": ["Fdm", "Sla", "Cnc", "Scanning", "Design"] 
    },
    "layerHeightMm": { "type": "number", "minimum": 0.01, "default": 0.2 },
    "supportEnabled": { "type": "boolean", "default": false },
    "heightMm": { "type": "number", "minimum": 0.1 },
    "scanningTier": { 
      "type": ["string", "null"], 
      "enum": ["RawScan", "ReverseEngineering", null] 
    },
    "quantity": { "type": "integer", "minimum": 1, "default": 1 },
    "geometry": { "$ref": "#/$defs/GeometryMetrics" },
    "correlationId": { "type": ["string", "null"] }
  },
  "$defs": {
    "GeometryMetrics": {
      "type": "object",
      "required": ["volumeCm3", "supportVolumeCm3", "surfaceAreaCm2", 
                   "boundingBoxX", "boundingBoxY", "boundingBoxZ", 
                   "isManifold", "triangleCount"],
      "properties": {
        "volumeCm3": { "type": "number", "minimum": 0.001 },
        "supportVolumeCm3": { "type": "number", "minimum": 0 },
        "surfaceAreaCm2": { "type": "number", "minimum": 0.01 },
        "boundingBoxX": { "type": "number", "minimum": 0.1 },
        "boundingBoxY": { "type": "number", "minimum": 0.1 },
        "boundingBoxZ": { "type": "number", "minimum": 0.1 },
        "isManifold": { "type": "boolean" },
        "triangleCount": { "type": "integer", "minimum": 1 }
      }
    }
  }
}
```

---

## Response Schema (JSON Schema)

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "required": ["strategy", "materialCost", "machineTimeCost", "setupCost",
               "totalUnitPrice", "totalPrice", "confidenceLevel", "validUntil"],
  "properties": {
    "strategy": { "type": "string", "enum": ["RuleBased", "Manual"] },
    "materialCost": { "type": "number", "minimum": 0 },
    "supportMaterialCost": { "type": "number", "minimum": 0 },
    "machineTimeCost": { "type": "number", "minimum": 0 },
    "setupCost": { "type": "number", "minimum": 0 },
    "complexitySurcharge": { "type": "number", "minimum": 0 },
    "subtotalBeforeMargin": { "type": "number", "minimum": 0 },
    "marginAmount": { "type": "number", "minimum": 0 },
    "totalUnitPrice": { "type": "number", "minimum": 0 },
    "totalPrice": { "type": "number", "minimum": 0 },
    "confidenceLevel": { "type": "number", "minimum": 0, "maximum": 1 },
    "validUntil": { "type": "string", "format": "date-time" },
    "calculationDuration": { "type": "string" },
    "notes": { "type": ["string", "null"] }
  }
}
```

---

## Backwards Compatibility

### ManufacturingProcessId (Optional)

For backwards compatibility, `manufacturingProcessId` and `manufacturingProcessName` remain in the request schema but are optional. If provided, they are ignored by the new pricing engine (technology is used instead).

```json
{
  "manufacturingProcessId": "3fa85f64-5717-4562-b3fc-2c963f66afa9",
  "manufacturingProcessName": "FDM Printing"
}
```

These fields will be deprecated in a future version.
