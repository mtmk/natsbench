using app_pipelines;

// Fast writes to a pipe, slow reads from a pipe buffering
// await new PipeReadWriteBuffers().Run();

// await new Completions().Run();

await new AdvanceExaminedAndCompleted().Run();
