using System.ClientModel.Primitives;
using Azure.AI.DocumentIntelligence;

namespace OcrAiVision.Infrastructure.UnitTests.DocumentIntelligence;

/// <summary>
/// Builds <see cref="AnalyzeResult"/> instances from the JSON the service
/// actually returns. Reading real response shapes back through the SDK's own
/// deserialiser keeps these fixtures honest: if the wire contract drifts, the
/// fixture stops parsing instead of quietly testing a shape that never occurs.
/// </summary>
internal static class AnalyzeResultFixtures
{
    public static AnalyzeResult FromJson(string json) =>
        ModelReaderWriter.Read<AnalyzeResult>(BinaryData.FromString(json))
        ?? throw new InvalidOperationException("The fixture JSON did not deserialise into an AnalyzeResult.");

    /// <summary>A two-page layout response with a table and a key/value pair.</summary>
    public const string LayoutResponse = """
    {
      "apiVersion": "2024-11-30",
      "modelId": "prebuilt-layout",
      "content": "PURCHASE ORDER\nItem Qty\nWidget 3",
      "pages": [
        {
          "pageNumber": 1,
          "lines": [ { "content": "PURCHASE ORDER" }, { "content": "Item Qty" } ],
          "words": [ { "content": "PURCHASE" }, { "content": "ORDER" }, { "content": "Item" }, { "content": "Qty" } ]
        },
        {
          "pageNumber": 2,
          "lines": [ { "content": "Widget 3" } ],
          "words": [ { "content": "Widget" }, { "content": "3" } ]
        }
      ],
      "tables": [
        {
          "rowCount": 2,
          "columnCount": 2,
          "boundingRegions": [ { "pageNumber": 2, "polygon": [0,0,1,0,1,1,0,1] } ],
          "cells": [
            { "kind": "columnHeader", "rowIndex": 0, "columnIndex": 0, "content": "Item" },
            { "kind": "columnHeader", "rowIndex": 0, "columnIndex": 1, "content": "Qty" },
            { "rowIndex": 1, "columnIndex": 0, "content": "Widget" },
            { "rowIndex": 1, "columnIndex": 1, "content": "3" }
          ]
        }
      ],
      "keyValuePairs": [
        {
          "key": { "content": "Order Number", "boundingRegions": [ { "pageNumber": 1, "polygon": [0,0,1,0,1,1,0,1] } ] },
          "value": { "content": "PO-4471" },
          "confidence": 0.91
        }
      ]
    }
    """;

    /// <summary>An invoice response carrying typed fields of several kinds.</summary>
    public const string InvoiceResponse = """
    {
      "apiVersion": "2024-11-30",
      "modelId": "prebuilt-invoice",
      "content": "TAX INVOICE",
      "pages": [ { "pageNumber": 1, "lines": [ { "content": "TAX INVOICE" } ], "words": [ { "content": "TAX" } ] } ],
      "documents": [
        {
          "docType": "invoice",
          "confidence": 0.95,
          "fields": {
            "VendorName": {
              "type": "string",
              "valueString": "Acme Trading",
              "content": "Acme Trading",
              "confidence": 0.93,
              "boundingRegions": [ { "pageNumber": 1, "polygon": [0,0,1,0,1,1,0,1] } ]
            },
            "InvoiceTotal": {
              "type": "currency",
              "valueCurrency": { "amount": 1200.5, "currencyCode": "ZAR", "currencySymbol": "R" },
              "confidence": 0.87
            },
            "InvoiceDate": {
              "type": "date",
              "valueDate": "2026-03-14",
              "confidence": 0.8
            },
            "IsPaid": {
              "type": "boolean",
              "valueBoolean": true,
              "confidence": 0.6
            },
            "PageCount": {
              "type": "integer",
              "valueInteger": 3
            }
          }
        }
      ]
    }
    """;

    /// <summary>A response with no pages, tables, fields or content.</summary>
    public const string EmptyResponse = """
    { "apiVersion": "2024-11-30", "modelId": "prebuilt-read", "content": "" }
    """;
}
