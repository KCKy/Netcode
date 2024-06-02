#!/usr/bin/env bats

load setup.bash
load functions.bash

@test "desync detection test 1" {
    prepare_log

    run_fast_1 server desync --server --magic 1 --duration 5 --target $SERVER_POINT
    server=$!
    sleep 1

    run_fast_1 client1 desync --magic 10 --target $TARGET
    client=$!

    run_fast_1 client2 desync --magic 20 --target $TARGET
    client=$!

    sleep 1

    run_fast_1 client3 desync --magic 30 --target $TARGET
    client=$!

    wait -n $server
    wait -n $client1
    wait -n $client2
    wait -n $client3
}

@test "desync detection test 3" {
    prepare_log

    run_fast_3 server desync --server --magic 1 --duration 5 --target $SERVER_POINT
    server=$!
    sleep 1

    run_fast_3 client1 desync --magic 10 --target $TARGET
    client=$!

    run_fast_3 client2 desync --magic 20 --target $TARGET
    client=$!

    sleep 1

    run_fast_3 client3 desync --magic 30 --target $TARGET
    client=$!

    wait -n $server
    wait -n $client1
    wait -n $client2
    wait -n $client3
}
