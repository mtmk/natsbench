package main

import (
	"context"
	"fmt"
	"github.com/nats-io/nats.go"
	"github.com/nats-io/nats.go/jetstream"
	"log"
	"time"
)

func main() {
	fmt.Println("NATS Bench Go Example")

	ctx, cancel := context.WithTimeout(context.Background(), 60*time.Minute)
	defer cancel()

	nc, err := nats.Connect("nats://127.0.0.1:4222")
	if err != nil {
		log.Fatal(err)
	}

	js, err := jetstream.New(nc)
	if err != nil {
		log.Fatal(err)
	}

	//s, err := js.CreateStream(ctx, jetstream.StreamConfig{
	//	Name:     "s1",
	//	Subjects: []string{"s1.*"},
	//})
	//if err != nil {
	//	log.Fatal(err)
	//}

	s, err := js.Stream(ctx, "s1")
	if err != nil {
		return
	}

	msg, err := s.GetMsg(ctx, 1)
	if err != nil {
		return
	}

	fmt.Println(msg.Subject, ":", msg.Sequence, ":", string(msg.Data))
}
