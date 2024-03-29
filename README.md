# SNORM
Simple .NET Object Relational Mapper (SNORM)

# MS SQL Only
This SQL API only works with Mircosoft SQL Server as it requires the use of table-valued parameters (TVP's).

## Users Table
![image](https://user-images.githubusercontent.com/23512394/275332705-b7f552bf-1872-461b-9e3d-d803eba86aba.png)

This is the simple user table I will reference in this article.

# The ORM
This API requires that the tables in your database have an primary key identity (auto-incremented) column to uniquely identify objects. This way auto generated queries can work properly and objects can be uniquely identified. This column does not have to be named **Id** but this is common. Also requires the user you are connecting with to have permissions to run SELECT, INSERT, UPDATE, DELETE, CREATE TYPE & DROP TYPE.

**If your table does not contain a primary key column that is auto-incremented (an identity) then the API will throw an error.**

### Basic Overview
Make a class that represents the users table. **The type name is very important here, as well as the property names.** The type name must match the table name and the **public** properties must match the column names. This is the basics of how this works. Just like JSON/XML serialization it just name matches to set values. I am sure I am trivializing how these work but I'm trying to make a simple comparison. :P

```C#
public class Users
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public int Age { get; set; }
    public string Email { get; set; }
}
```

After we have created our type to use we can then just very easily manage our runtime objects instead of worrying about SQL...obviously.

```C#
string connectionString = "...";

SqlDatabase db = new SqlDatabase(connectionString);
db.Connect();

Users me = new Users
{
    FirstName = "Yoda",
    LastName = "DoesYodaEvenHaveALastName",
    Age = 900,
    Email = "heisyoda@gmail.com"
};

int results = db.Insert(new List<Users>() { me });

// we select the object because we need the id
List<Users> users = db.Select<Users>();

me = users[0];

Debug.WriteLine($"My id after being inserted:{me.Id}");

// Yoda had a birthday!!!
me.Age = 901;

db.Update(new List<Users>() { me });

db.Delete(new List<Users>() { me });
```

Let's address the comment above the **.Select** method call. This is because the API uses TVP's to INSERT, UPDATE and DELETE multiple records in one round-trip to the database. We are unable to query IDENT_CURRENT when inserting data because of the bulk operation. So it is up to the developer to pull the objects back out of the database. *In this example my Id in the users table is an identity and is incremented by one every time*. A note on this is a minute.

### Auto-Incremented (Identity) Columns
The API queries appropriate information from SQL Server to interpret column metadata for the tables in the specified database. This allows it to track identity columns, also commonly referred to as auto-incrementing columns.

In SQL server you can have a column in a table be an auto-incremented and you can choose the increment value. This is not a tutorial on identity columns in SQL Server so that's all I'll say. These auto-incremented or identity columns are not included in the auto generated INSERT query. This is because the database engine will take care of this for us. These auto generated values come from the database engine and you can query what it is for a table by running IDENT_CURRENT. So if we inserted one record at a time we could query IDENT_CURRENT each time and get that value back to set on the Id property but because we bulk INSERT we cannot do that. So...as aforementioned it is up to the developer to manage this problem on their own by simply running a SELECT statement immediately following. This API is obviously more efficient with larger sets of data.

This Id (or whatever you call it) column MUST be a primary key AND must be an identity (auto-incrementing). More on the specifics of this in a minute.

### Round Trips
The API needs to create a type that will be our TVP that represents our table. Meaning the auto generated TVP type will exactly match table it is interacting with, except in the type generated for INSERT. INSERT auto generated TVPs will exclude identity columns. This is because we don't need to tell the database what this value will be...it's auto generated by the engine (as I already mentioned). Auto-incremented columns are used in the WHERE clause of the UPDATE statement but they are not updated.

All that being said the API does this...
- first round trip to create TVP type
- second round trip runs the auto generated query
- third round trip drops (deletes) the type created

So if you are inserting one record the API is not more efficient then just running a query with the .NET type SqlCommand by any means but it's not going to be much slower either. This API pays dividends in time when needing to insert more than 3 records, right? If you are inserting 4 records than the API has less round trips now. However there is more to SQL efficiency than the number of round trips but from a client application accessing a remote database it can be a major factor.

### Auto Generated Queries and Types
#### Queries
Knowing how the API works and what kind of queries and types it generates can help in understanding how it works. The API tries to qualify as much of the data as possible when running queries. Meaning it uses as many columns as it can to uniquely specify data. Again, it will exclude auto-incremented columns in the INSERT query and they will ONLY be used in the WHERE clause of the UPDATE query. Also for clarity sake, DELETE uses every column for record identification/matching.

What the DELETE query will look like when running DELETE -> 
```SQL
DELETE FROM dbo.Users WHERE EXISTS (SELECT tvp.* FROM @tvp AS tvp)
```
What the INSERT query will look like when running INSERT -> 
```SQL
INSERT INTO dbo.Users (FirstName,LastName,Age,Email) SELECT tvp.FirstName,tvp.LastName,tvp.Age,tvp.Email FROM @TVP AS tvp
```
What the UPDATE query will look like when running UPDATE -> 
```SQL
UPDATE dbo.Users SET Users.FirstName = tvp.FirstName,Users.LastName = tvp.LastName,Users.Age = tvp.Age,Users.Email = tvp.Email FROM Users INNER JOIN @tvp AS tvp ON Users.Id = tvp.Id
```

If your table contains a multi-column primary key this is fine. One of those columns MUST be an identity column. All of those primary key columns will be referenced in the WHERE clause of the UPDATE statememt. I just only happen to have the Id column as my primary or else you'd see **'tvp.Email FROM Users INNER JOIN @tvp AS tvp ON Users.Id = tvp.Id AND Users.WhateverTheColumnNameIs = tvp.WhateverTheColumnNameIs'**.

#### Types
When the API creates types it does what it can to match the table layout as much as possible. Here is what it generates for my user table TVP type for the INSERT query->
```SQL
CREATE TYPE dbo.UsersAsTvp AS TABLE (FirstName VarChar (50),LastName VarChar (50),Age Int,Email VarChar (250))
```
And this is what it generates for DELETE and UPDATE ->
```SQL
CREATE TYPE dbo.UsersAsTvp AS TABLE (Id Int,FirstName VarChar (50),LastName VarChar (50),Age Int,Email VarChar (250))
```

Notice how it includes auto-incremented columns in the types for these queries. Again, this is because they are used.

### Select\<T>() vs Select\<T>(string...)
The basic *Select* method will select all the records from the table that matches the type name exactly. However if you want to run a custom SELECT statement then you can by using one of the overloads for the *Select* method. This way you can just return any data you want. Here is where some of the power of the API and the generic name matching comes in handy for populating data objects that don't represent tables but rather whatever result set you want. With a mix of power from the SQL side with aliasing returned columns as whatever you want (SELECT Id AS Identification, FirstName AS FN, etc. FROM Users...). Now lets consider a situation where you are returning a result set from a query that joins multiple tables together and selects data from those multiple tables. This won't map to a type name. **So for *Select* overloads that take in a query** we don't match the name of the type to a table name but rather just the columns and the properties.

### Override Name Matching
With everything being said about the type name needing to match the table name and the property names needing to match the column names, you can override this with the use of 2 attributes, SqlTableAttribute and SqlColumnAttribute. Pretty self explanatory but the SqlTableAttribute can only be used on a class and the SqlColumnAttribute can only be used on a property. This simply allows you to alias the name used when the API references the type or its properties.

```C#
[SqlTableAttribute("WhateverYouWant")]
public class Users
{
    [SqlColumnAttribute("SomeColumnName")]
    public int Id { get; set; }

    [SqlColumnAttribute("SomeColumnName2")]
    public string FirstName { get; set; }
    
    [SqlColumnAttribute("SomeColumnName3")]
    public string LastName { get; set; }
    
    [SqlColumnAttribute("SomeColumnName4")]
    public int Age { get; set; }
    
    [SqlColumnAttribute("SomeColumnName5")]
    public string Email { get; set; }
}
```

So now *WhateverYouWant* will be used in the generated queries and types as the table name and *SomeColumnName#* will be used in the generated queries and types as the column names.

### General Tips for Usage
Understand your data and how these queries will affect your data. We try to qualify the operations as much as possible (again, use as many columns as we can) to avoid updating and deleting multiple records at the same time. However if you have a poorly designed database it may do just that. So it's up to you to design good normalized databases. :P 

# The SimpleSqlService
Included in the API is a simple static class called SimpleSqlService and this class provides two methods, ExecuteNonQuery and ExecuteQuery.

### ExecuteNonQuery
This method has 6 parameters; |SqlConnection connection, bool autoConnect, Action<string> log, string query, CommandType commandType, params SqlParameter[] parameters| and it returns the number of rows affected just like *SqlCommand.ExecuteNonQuery*.

### ExecuteQuery
This method has 6 parameters; |SqlConnection connection, bool autoConnect, Action<string> log, string query, CommandType commandType, params SqlParameter[] parameters| and it returns a jagged object array containing the results of your query. Each object[] in the collection of object arrays  will represent a "row" of data in the result set. Each value in the row represents a "column" value. Know the order of the data being returned from the query horizontally (column order). So if you wanted to access row 5 column 3 you'd do returnedArray[4][2] and this will return the object that sits at that location, even if null. Check this yourself. **You will have to unbox the data yourself** because everything is returned as an object.

# The SqlInformationService
The API also comes with another simple static class called SqlInformationService and the only method this class has is *GetTableInformation*. This will retrieve column metadata information for a table. :) This method has 4 parameters; |SqlConnection connection, string tableSchema, string tableName, Action<string> log|

# What this API is Not
This API is not the Entity Framework or other heavier weight ORM's that track and manage foreign key relationships and objects. The SqlColumn[] returned from the *SqlInformationService.GetTableInformation* method will tell you if a column is a foreign key and give you the metadata about that foreign key (parent table, parent table column, child table, child table column and the name of the relationship itself) and you can use this to manage the problem yourself. Good luck!
