# SQL Examples

## Listing Maps with Pipelines and resolved variables
```sql

/*

  Listing Maps with Pipelines and resolved variables

*/

SELECT   m.[Id]
      ,m.[Description]
      ,m.[Package]
      ,m.[RefId]
	  ,'Pipeline' =   CASE
		  WHEN SUBSTRING(m.[RefId], 1, LEN(m.[RefId]) - CHARINDEX('\', REVERSE(m.[RefId]))) = 'Package' THEN m.[RefId]
		  ELSE SUBSTRING(m.[RefId], 1, LEN(m.[RefId]) - CHARINDEX('\', REVERSE(m.[RefId]))) 
		  END

      ,'SQL Source' = m.[SqlStatement]
	  ,'Resolved SQL' = CASE 
		  WHEN v.VariableValue  IS NULL THEN m.SqlStatement
		  ELSE v.VariableValue
		  END
      ,m.[ConnectionString]
      ,m.[ConnectionName]
      ,m.[ConnectionDtsId]
      ,m.[ConnectionType]
      ,m.[ConnectionRefId]
      ,m.[Name]
      ,m.[ComponentType]
      ,m.[LoadDate]
  FROM [dbo].[DTSX_Mapper] m
  LEFT JOIN [dbo].[DTSX_Variables] v on v.Package=m.Package and CONCAT(v.[VariableNameSpace],'::',v.[VariableName]) = m.SqlStatement

/*

Show all attributes associated with a Package's Connections

*/

   SELECT  a.[Package]
	  ,'Connection Name' = e.ParentRefId
      ,a.[ParentNodeName]
	        ,'Element Parent UniqueId' =e.[ParentUniqueId]
      ,'Element UniqueId' =e.[UniqueId]
      ,'Attr Parent UniqueId' =a.[ParentUniqueId]
      ,'Attr Parent UniqueId' =a.[UniqueId]
      ,a.[RefId]
      ,a.[XPath]
      ,a.[AttributeName]
      ,a.[AttributeType]
      ,a.[AttributeValue]
      ,a.[LoadDate]
  FROM [dbo].[DTSX_Attributes] a
  LEFT JOIN [dbo].[DTSX_Elements] e on e.Package=a.Package and a.ParentUniqueId=e.UniqueId and e.ParentNodeName=a.ParentNodeName
  
  WHERE a.Package='tablevVarNameDepartment.dtsx'  and e.ParentNodeName='DTS:ConnectionManagers'
    

