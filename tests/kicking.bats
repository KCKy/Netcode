#!/usr/bin/env bats

load setup.bash
load functions.bash

@test "kicking test 1" {
    prepare_log

    run_fast_1 server kick --server --duration 200 --target $SERVER_POINT
    server=$!
    sleep 1

    run_fast_1 client1 kick --target $TARGET
    client1=$!

    run_fast_1 client2 kick --target $TARGET
    client2=$!

    sleep 1

    run_fast_1 client3 kick --target $TARGET
    client3=$!

    run_fast_1 client4 kick --target $TARGET
    client4=$!

    wait -n $server
    wait -n $client1
    wait -n $client2
    wait -n $client3
    wait -n $client4
}

@test "kicking test 2" {
    prepare_log

    run_fast_2 server kick --server --duration 200 --target $SERVER_POINT
    server=$!
    sleep 1

    run_fast_2 client1 kick --target $TARGET
    client1=$!

    run_fast_2 client2 kick --target $TARGET
    client2=$!

    sleep 1

    run_fast_2 client3 kick --target $TARGET
    client3=$!

    run_fast_2 client4 kick --target $TARGET
    client4=$!

    wait -n $server
    wait -n $client1
    wait -n $client2
    wait -n $client3
    wait -n $client4
}

@test "kicking test 3" {
    prepare_log

    run_fast_3 server kick --server --duration 200 --target $SERVER_POINT
    server=$!
    sleep 1

    run_fast_3 client1 kick --target $TARGET
    client1=$!

    run_fast_3 client2 kick --target $TARGET
    client2=$!

    sleep 1

    run_fast_3 client3 kick --target $TARGET
    client3=$!

    run_fast_3 client4 kick --target $TARGET
    client4=$!

    wait -n $server
    wait -n $client1
    wait -n $client2
    wait -n $client3
    wait -n $client4
}

@test "kicking test 4" {
    prepare_log

    run_fast_4 server kick --server --duration 200 --target $SERVER_POINT
    server=$!
    sleep 1

    run_fast_4 client1 kick --target $TARGET
    client1=$!

    run_fast_4 client2 kick --target $TARGET
    client2=$!

    sleep 1

    run_fast_4 client3 kick --target $TARGET
    client3=$!

    run_fast_4 client4 kick --target $TARGET
    client4=$!

    wait -n $server
    wait -n $client1
    wait -n $client2
    wait -n $client3
    wait -n $client4
}
