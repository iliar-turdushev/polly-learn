bool isServiceCollectionDemo = true;

if (isServiceCollectionDemo)
{
    await PollyDemoServiceCollection.ExecuteAsync();
}
else
{
    await PollyDemo.ExecuteAsync();
}