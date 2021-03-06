# MassTransit Legacy Gateway

Very often we create new shiny microservices and integrate them using messaging, 
but there's nearly always an elephant in the room - the _legacy system_.

If we decide to use a [Strangler Application](https://www.martinfowler.com/bliki/StranglerApplication.html)
pattern, we need to be able to integrate the legacy system with our new microservices
landscape. Usually, there is little control over data changes across chaotically organized,
database-centric systems. To be absolutely sure that we catch and publish all the changes
that happen inside the legacy system, we must look at the database changes and
figure our how to bridge these changes to messaging.

The Gateway purpose is exactly this. You can use it to publish messages from 
stored procedures and database triggers, ensuring that your messages consistently
reflect all changes in the database of a legacy system.

Steps you need to do:
 - Create a message queue table
 - Use SQL in a stored procedure or trigger to insert new rows to the queue table
 - Set up a gateway instance, pointing to that table
 
The gateway will poll the queue table periodically and for each new row it will
publish a new message to the message bus. The gateway is using MassTransit
so it always follows the topology rules. It builds the message types dynamically, 
so you cannot use inheritance for your message contracts.

# Queue tables

There are two types of queue tables that the gateway support.

## Single-message-type queue table

For this type of queue table, you have one table per message type. Each column
in the table represents a message class property. The property type will be
derived from the SQL column type.

Using this method is easy, since all you need to do is insert new rows to that
table, using the vanilla SQL syntax.

The table **must** have a field called `RowNumber` of type`int` that is also a primary key.

## Message JSON queue table

This method uses one table for messages of any type. Such table must have three 
columns:
 - `RowNumber` (type `int`)
 - `Timestamp` (type `datetime`)
 - `MessageType` (type `varchar`)
 - `Payload` (type `text`)
 
The `MessageType` column must contain the full CRL type of your message that
your consumers are subscribing to.

The content of your message must be in the `Payload` column, formatted as a 
valid JSON. The JSON object must be flat and can't contain complex objects or arrays.

For example:

| Timestamp | MessageType | Payload |
|-----------|-------------|---------|
| 2018-09-01Z10:00:01 | OrderService.OrderRegistered | { "orderId": 231, "customerName": "Apple" } |
| 2018-09-01Z10:00:02 | PaymentService.PaymentProcessed | { "paymentId": 223, "amount": 2354.12 } |

The gateway will create message types dynamically from the JSON schema, so it is
important that all fields in the schema are included in each message.

# Databases

Currently, there is a core project and MS SQL Server support are implemented.

## Microsoft SQL Server

You can add the gateway to MassTransit bus configuration using extension methods.

Install the NuGet [package](https://www.nuget.org/packages/MassTransit.Gateway.SqlServer)
 `MassTransit.Gateway.SqlServer`.

### Table per message type

Example SQL statement:
```sql
CREATE TABLE [dbo].[CustomerNameChangedQueue] (
  [RowNumber] int PRIMARY KEY IDENTITY,
  [CustomerId] int,
  [CustomerName] varchar(200),
)
```

When you put new rows into this table, it will publish messages like:

```json
{
  "CustomerId": 10,
  "CustomerName": "Apple"
}
```

You need to add the gateway like this, where you specify the full
CLR type name for the event, which your consumers are subscribed to:

```csharp
var bus = Bus.Factory.CreateUsingRabbitMq(cfg =>
{
    cfg.Host(new Uri("rabbitmq://localhost"), h =>
    {
        h.Username("guest");
        h.Password("guest");
    });
    
    cfg.AddMessageTableSqlServerGateway(
        "OrderQueueTable",                                  // table name
        () => new SqlConnection(Settings.ConnectionString), // connection factory
        "OrderService.OrderPlaced",                         // full message type name
        TimeSpan.FromSeconds(1));                           // polling interval
});
```

### Universal JSON queue table

Here is the sample SQL statement:

```sql
CREATE TABLE [dbo].[JsonQueue] (
  [RowNumber] int PRIMARY KEY IDENTITY,
  [MessageType] varchar(200),
  [Timestamp] datetime default CURRENT_TIMESTAMP,
  [Payload] text
)
```
 
You register the gateway differently here, no message type is needed:

```csharp
var bus = Bus.Factory.CreateUsingRabbitMq(cfg =>
{
    cfg.Host(new Uri("rabbitmq://localhost"), h =>
    {
        h.Username("guest");
        h.Password("guest");
    });
    
    cfg.AddJsonQueueTableSqlServerGateway(
        "OrderQueueTable",                                  // table name
        () => new SqlConnection(Settings.ConnectionString), // connection factory
        TimeSpan.FromSeconds(1));                           // polling interval
});
```

