#!/usr/bin/env bats

setup()
{
    export TESTER='../src/Tester/bin/Release/net8.0/Tester.dll'
    export SERVER_POINT='127.0.0.1:12356'
    export TARGET='127.0.0.1:12356'
}

prepare_log()
{
    [ -n "$BATS_TEST_DIRNAME" ] || exit
    cd "$BATS_TEST_DIRNAME" || exit

    rm -rd "./$BATS_TEST_NUMBER"
    mkdir $BATS_TEST_NUMBER

    logdir="$BATS_TEST_DIRNAME/$BATS_TEST_NUMBER"
}

run()
{
    tag=$1
    shift 1
    dotnet "$TESTER" --delta 0.05 --sample-window 20 --trace --checksum --comlog "$logdir/${tag}_com.log" --log "$logdir/${tag}_test.log" --gamelog "$logdir/${tag}_game.log" $@ &
}

@test "basic test" {
    prepare_log
    run server rec --tickrate 20 --server --game-duration 200 --target $SERVER_POINT
    server=$!
    sleep 2
    
    run client1 rec --tickrate 20 --duration 160 --warmup 30 --target $TARGET
    client1=$!

    run client2 rec --tickrate 20 --duration 160 --warmup 30 --target $TARGET
    client2=$!

    run client3 rec --tickrate 20 --duration 160 --warmup 30 --target $TARGET
    client3=$!

    run client4 rec --tickrate 20 --duration 160 --warmup 30 --target $TARGET
    client4=$!

    run client5 rec --tickrate 20 --duration 160 --warmup 30 --target $TARGET
    client5=$!

    wait -n $server
    wait -n $client1
    wait -n $client2
    wait -n $client3
    wait -n $client4
    wait -n $client5
}
