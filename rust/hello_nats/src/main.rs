// use futures::StreamExt;
// use async_nats::jetstream;

#[tokio::main]
async fn main() -> Result<(), async_nats::Error> {

    // let client = async_nats::connect("localhost").await?;
    // let mut subscriber = client.subscribe("messages".into()).await?.take(2);

    // for _ in 0..2 {
    //     client.publish("messages".into(), "data".into()).await?;
    // }

    // while let Some(message) = subscriber.next().await {
    //   println!("Received message {:?}", message);
    // }

    // Ok(())

    let client = async_nats::connect("localhost").await?;
    let jetstream = async_nats::jetstream::new(client);

    let _stream = jetstream.get_stream("events").await?;

    let ack = jetstream.publish("events".to_string(), "data1".into()).await?;
    ack.await?;
    
    // jetstream.publish("events".to_string(), "data2".into())
      // .await?
      // .await?;

    Ok(())
  }